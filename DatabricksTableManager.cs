# Databricks / PySpark notebook
# Purpose: analyze fact + dimension relationships and classify columns into
#          CORE (keep in main dimension) vs AGG_CANDIDATE (move to aggregation/ext dim)
# Author: Julio Diaz
# ------------------------------------------------------------------------------

from pyspark.sql import functions as F
import re

# =============================================================================
# CONFIGURATION
# =============================================================================

fact_table = "pbi_synpoc2.fact_risk_results_v2"  # <-- update to your fact table
dimensions = [
    {"name": "pbi_synpoc2.dim_position",  "fact_fk": "pbi_pos_id",  "dim_key": "pbi_pos_id"},
    {"name": "pbi_synpoc2.dim_product",   "fact_fk": "pbi_prod_id", "dim_key": "pbi_prod_id"},
    # Add more dimensions here as needed
]

# Exclude technical columns
EXCLUDE_RE = re.compile(r"(?i)^(row_.*|.*_id|.*_key|hash.*|create.*|update.*|insert.*|_.*)$")

# Optional sampling for large facts
FACT_SAMPLE = None   # e.g., 0.1 for 10% sample

MAX_CARD_FOR_SCORING = 2000  # skip columns above this cardinality (too unique)

# =============================================================================
# POLICY THRESHOLDS (you can tune)
# =============================================================================
CORE_MIN_CONTRIB        = 0.35
CORE_MIN_COVERAGE       = 0.70
CORE_MAX_DISTINCT_RATIO = 0.50
CORE_MAX_NULL_RATE      = 0.40

AGG_MAX_CONTRIB         = 0.15
AGG_MIN_NULL_RATE       = 0.30
AGG_MIN_DISTINCT_RATIO  = 0.10

# =============================================================================
# LOAD FACT TABLE
# =============================================================================
fact_df = spark.table(fact_table)
if FACT_SAMPLE and 0 < FACT_SAMPLE < 1:
    fact_df = fact_df.sample(False, FACT_SAMPLE, seed=42)

total_fact_rows = fact_df.count()
if total_fact_rows == 0:
    raise ValueError("Fact table is empty or sampling too low.")

def log2_col(c):
    return F.log(c) / F.log(F.lit(2.0))

dimension_scores = []
column_recos = []

# =============================================================================
# MAIN LOOP
# =============================================================================
for dim in dimensions:
    dim_name = dim["name"]
    fk = dim["fact_fk"]
    dk = dim["dim_key"]

    dim_df = spark.table(dim_name)
    dim_schema = dict(dim_df.dtypes)

    # Columns to analyze (exclude keys/technical)
    candidate_cols = [c for c in dim_df.columns if c.lower() != dk.lower() and not EXCLUDE_RE.match(c)]

    # ---- Alias fix: avoid ambiguous column names ----
    dim_key_alias = f"__{dk}_dim"
    dim_col_alias = {c: f"__d_{c}" for c in candidate_cols}

    dim_sel = [F.col(dk).alias(dim_key_alias)] + [F.col(c).alias(dim_col_alias[c]) for c in candidate_cols]
    f = fact_df.alias("f")
    d = dim_df.select(*dim_sel).alias("d")

    joined = f.join(d, F.col(f"f.{fk}") == F.col(f"d.{dim_key_alias}"), "left").cache()

    matched_rows = joined.where(F.col(dim_key_alias).isNotNull()).count()
    coverage_pct = matched_rows / total_fact_rows if total_fact_rows else 0.0
    fk_null_rows = fact_df.where(F.col(fk).isNull()).count()
    fk_distinct = fact_df.select(fk).distinct().count()
    avg_fact_per_dim_member = matched_rows / fk_distinct if fk_distinct else None

    if matched_rows == 0:
        dimension_scores.append({
            "dimension": dim_name,
            "coverage_pct": 0.0,
            "best_column": None,
            "best_score": 0.0
        })
        continue

    matched = joined.where(F.col(dim_key_alias).isNotNull()).cache()
    matched_count = matched_rows

    for c in candidate_cols:
        c_alias = dim_col_alias[c]

        approx_card = matched.agg(F.approx_count_distinct(F.col(c_alias)).alias("card")).first()["card"]
        if not approx_card or approx_card <= 1 or approx_card > MAX_CARD_FOR_SCORING:
            continue

        dist = matched.groupBy(F.col(c_alias)).agg(F.count("*").alias("cnt")).withColumn("p", F.col("cnt") / matched_count)
        entropy = dist.select((-F.sum(F.col("p") * log2_col(F.col("p")))).alias("entropy")).first()["entropy"]
        max_entropy = (F.log(F.lit(approx_card)) / F.log(F.lit(2.0))).collect()[0][0]
        norm_entropy = float(entropy) / float(max_entropy) if max_entropy else 0.0

        null_rate = matched.select(F.count(F.when(F.col(c_alias).isNull(), 1)).alias("nulls")).first()["nulls"] / matched_count
        distinct_ratio = approx_card / matched_count

        contrib = float(coverage_pct) * float(norm_entropy)

        # ---- Classification ----
        is_core = (
            contrib >= CORE_MIN_CONTRIB and
            coverage_pct >= CORE_MIN_COVERAGE and
            distinct_ratio <= CORE_MAX_DISTINCT_RATIO and
            null_rate <= CORE_MAX_NULL_RATE
        )

        is_agg = (
            contrib < AGG_MAX_CONTRIB or
            null_rate >= AGG_MIN_NULL_RATE or
            distinct_ratio <= AGG_MIN_DISTINCT_RATIO
        )

        label = "CORE" if is_core and not is_agg else "AGG_CANDIDATE" if is_agg else "NEUTRAL"

        column_recos.append({
            "dimension": dim_name,
            "column": c,
            "approx_cardinality": int(approx_card),
            "coverage_pct": coverage_pct,
            "normalized_entropy": norm_entropy,
            "contribution_score": contrib,
            "null_rate": null_rate,
            "distinct_ratio": distinct_ratio,
            "recommendation": label
        })

    # Best column for dimension summary
    best_col = max(
        [cr for cr in column_recos if cr["dimension"] == dim_name],
        key=lambda x: x["contribution_score"],
        default=None
    )

    dimension_scores.append({
        "dimension": dim_name,
        "coverage_pct": coverage_pct,
        "best_column": best_col["column"] if best_col else None,
        "best_score": best_col["contribution_score"] if best_col else 0.0
    })

# =============================================================================
# OUTPUTS
# =============================================================================
dim_df = spark.createDataFrame(dimension_scores)
col_df = spark.createDataFrame(column_recos)

# 1️⃣ Dimension-level ranking
display(dim_df.orderBy(F.col("best_score").desc()))

# 2️⃣ Column-level detailed ranking
display(col_df.orderBy(F.col("contribution_score").desc()))

# 3️⃣ Aggregation candidates
agg_candidates = col_df.where(F.col("recommendation") == "AGG_CANDIDATE").orderBy(F.col("contribution_score").asc())
display(agg_candidates)

# 4️⃣ Core dimension columns
core_cols = col_df.where(F.col("recommendation") == "CORE").orderBy(F.col("contribution_score").desc())
display(core_cols)

print("""
✅ Interpretation:
- CORE → keep in primary dimension (joined to DirectQuery detail).
- AGG_CANDIDATE → move to smaller, low-cardinality dimension (linked to Import aggregation table).
Tune thresholds in the policy section to refine behavior.
""")
