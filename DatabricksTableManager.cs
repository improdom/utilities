# Databricks / PySpark
from pyspark.sql import functions as F, types as T
from pyspark.sql import Window
import re

# ======================================
# CONFIGURE YOUR MODEL
# ======================================
fact_table = "your_schema.fact_results"

# Each dim: name (table), fact_fk (FK in fact), dim_key (PK in dim)
dimensions = [
    {"name": "your_schema.dim_position",  "fact_fk": "position_id",   "dim_key": "position_id"},
    {"name": "your_schema.dim_product",   "fact_fk": "product_id",    "dim_key": "product_id"},
    {"name": "your_schema.dim_customer",  "fact_fk": "customer_id",   "dim_key": "customer_id"},
    # add more...
]

# Exclude technical columns from scoring
EXCLUDE_RE = re.compile(r"(?i)^(row_.*|.*_id|.*_key|hash.*|create.*|update.*|insert.*|_.*)$")

# Sampling for speed (None to disable)
FACT_SAMPLE = None  # e.g. 0.1

# Scoring guardrails
MAX_CARD_FOR_SCORING = 2000  # skip columns above this (too unique to be good slicers)

# ======================================
# POLICY / THRESHOLDS (TUNE FREELY)
# ======================================
CORE_MIN_CONTRIB        = 0.35   # column contribution >= -> keep in core
CORE_MIN_COVERAGE       = 0.70   # dimension coverage >= -> stronger preference for core
CORE_MAX_DISTINCT_RATIO = 0.50   # if a column is >50% distinct, it’s too close to per-row detail
CORE_MAX_NULL_RATE      = 0.40   # very sparse columns default away from core

AGG_MAX_CONTRIB         = 0.15   # column contribution < -> candidate for ext/aggregation table
AGG_MIN_NULL_RATE       = 0.30   # null heavy? good candidate to move out
AGG_MIN_DISTINCT_RATIO  = 0.10   # very low segmentation (near-constant) -> move out

# ======================================
# LOAD FACT
# ======================================
fact_df = spark.table(fact_table)
if FACT_SAMPLE and 0 < FACT_SAMPLE < 1:
    fact_df = fact_df.sample(withReplacement=False, fraction=FACT_SAMPLE, seed=42)

total_fact_rows = fact_df.count()
if total_fact_rows == 0:
    raise ValueError("Fact table is empty (after sampling).")

def log2_col(c):
    return F.log(c) / F.log(F.lit(2.0))

all_dim_scores = []
all_col_scores = []
recommendations = []

for dim in dimensions:
    dim_name = dim["name"]
    fk = dim["fact_fk"]
    dk = dim["dim_key"]

    dim_df = spark.table(dim_name)

    # Candidate columns: non-key, not excluded
    candidate_cols = [c for c in dim_df.columns if c.lower() != dk.lower() and not EXCLUDE_RE.match(c)]

    # Join fact -> dim
    joined = (
        fact_df.join(dim_df.select(dk, *candidate_cols), fact_df[fk] == dim_df[dk], "left")
    ).cache()

    matched_rows = joined.where(F.col(dk).isNotNull()).count()
    coverage_pct = matched_rows / total_fact_rows if total_fact_rows else 0.0

    fk_null_rows = fact_df.where(F.col(fk).isNull()).count()
    fk_distinct = fact_df.select(fk).distinct().count()
    avg_fact_per_dim_member = (matched_rows / fk_distinct) if fk_distinct > 0 else None

    if matched_rows == 0:
        all_dim_scores.append({
            "dimension": dim_name,
            "coverage_pct": 0.0,
            "fk_null_rate": fk_null_rows / total_fact_rows if total_fact_rows else None,
            "fk_distinct": fk_distinct,
            "avg_fact_per_dim_member": avg_fact_per_dim_member,
            "best_column": None,
            "best_column_score": 0.0
        })
        continue

    matched = joined.where(F.col(dk).isNotNull()).cache()
    matched_count = matched_rows

    # Precompute dtypes for cheap checks
    dim_schema = dict(dim_df.dtypes)  # {col: datatype_str}

    col_scores = []
    col_recos = []

    for c in candidate_cols:
        # Approx cardinality (skip 1 or huge)
        approx_card = matched.agg(F.approx_count_distinct(F.col(c)).alias("card")).first()["card"]
        if not approx_card or approx_card <= 1 or approx_card > MAX_CARD_FOR_SCORING:
            continue

        # Distribution for entropy
        dist = matched.groupBy(F.col(c)).agg(F.count(F.lit(1)).alias("cnt"))
        dist = dist.withColumn("p", F.col("cnt") / F.lit(matched_count))

        entropy = dist.select((-F.sum(F.col("p") * (log2_col(F.col("p"))))).alias("entropy")).first()["entropy"]
        max_entropy = (log2_col(F.lit(approx_card))).collect()[0][0]
        norm_entropy = float(entropy) / float(max_entropy) if max_entropy and max_entropy > 0 else 0.0

        # Null rate on matched rows
        null_rate = matched.select(F.count(F.when(F.col(c).isNull(), 1)).alias("nulls")).first()["nulls"] / matched_count

        # Distinct ratio vs matched rows
        distinct_ratio = approx_card / matched_count

        # Avg string length (storage proxy) if string
        dtype = dim_schema.get(c)
        avg_strlen = None
        if dtype and dtype.lower().startswith("string"):
            avg_strlen = matched.select(F.avg(F.length(F.col(c))).alias("avg_len")).first()["avg_len"]

        # Contribution score (same as earlier): coverage * normalized entropy
        contribution = float(coverage_pct) * float(norm_entropy)

        entry = {
            "dimension": dim_name,
            "column": c,
            "approx_cardinality": int(approx_card),
            "coverage_pct": float(coverage_pct),
            "normalized_entropy": float(norm_entropy),
            "contribution_score": float(contribution),
            "null_rate": float(null_rate),
            "distinct_ratio": float(distinct_ratio),
            "avg_strlen": float(avg_strlen) if avg_strlen is not None else None,
            "dtype": dtype
        }
        col_scores.append(entry)

        # -----------------------------
        # CLASSIFICATION: CORE vs AGG_CANDIDATE
        # -----------------------------
        reasons = []

        is_core = (
            (entry["contribution_score"] >= CORE_MIN_CONTRIB) and
            (entry["coverage_pct"]       >= CORE_MIN_COVERAGE) and
            (entry["distinct_ratio"]     <= CORE_MAX_DISTINCT_RATIO) and
            (entry["null_rate"]          <= CORE_MAX_NULL_RATE)
        )

        is_agg_candidate = (
            (entry["contribution_score"] < AGG_MAX_CONTRIB) or
            (entry["null_rate"]          >= AGG_MIN_NULL_RATE) or
            (entry["distinct_ratio"]     <= AGG_MIN_DISTINCT_RATIO)
        )

        label = "NEUTRAL"
        if is_core and not is_agg_candidate:
            label = "CORE"
            if entry["contribution_score"] < CORE_MIN_CONTRIB: reasons.append("contribution below core min")
            if entry["coverage_pct"] < CORE_MIN_COVERAGE: reasons.append("coverage below core min")
            if entry["distinct_ratio"] > CORE_MAX_DISTINCT_RATIO: reasons.append("too high distinct_ratio for core")
            if entry["null_rate"] > CORE_MAX_NULL_RATE: reasons.append("too sparse for core")
        elif is_agg_candidate and not is_core:
            label = "AGG_CANDIDATE"
            if entry["contribution_score"] < AGG_MAX_CONTRIB: reasons.append("low contribution")
            if entry["null_rate"] >= AGG_MIN_NULL_RATE: reasons.append("high null rate")
            if entry["distinct_ratio"] <= AGG_MIN_DISTINCT_RATIO: reasons.append("very low segmentation (near-constant)")
        else:
            # tie or middle ground
            reasons.append("borderline metrics; review usage")

        entry_reco = {
            **entry,
            "recommendation": label,
            "reasons": ", ".join(reasons) if reasons else None
        }
        col_recos.append(entry_reco)

    # Save per-dimension rollup
    if col_scores:
        best_col = max(col_scores, key=lambda x: x["contribution_score"])
        all_dim_scores.append({
            "dimension": dim_name,
            "coverage_pct": float(coverage_pct),
            "fk_null_rate": fk_null_rows / total_fact_rows if total_fact_rows else None,
            "fk_distinct": fk_distinct,
            "avg_fact_per_dim_member": avg_fact_per_dim_member,
            "best_column": best_col["column"],
            "best_column_score": best_col["contribution_score"]
        })
        all_col_scores.extend(col_scores)
        recommendations.extend(col_recos)
    else:
        all_dim_scores.append({
            "dimension": dim_name,
            "coverage_pct": float(coverage_pct),
            "fk_null_rate": fk_null_rows / total_fact_rows if total_fact_rows else None,
            "fk_distinct": fk_distinct,
            "avg_fact_per_dim_member": avg_fact_per_dim_member,
            "best_column": None,
            "best_column_score": 0.0
        })

# ======================================
# OUTPUTS
# ======================================
dims_df = spark.createDataFrame(all_dim_scores)
cols_df = spark.createDataFrame(all_col_scores) if all_col_scores else spark.createDataFrame([], "dimension STRING, column STRING, approx_cardinality INT, coverage_pct DOUBLE, normalized_entropy DOUBLE, contribution_score DOUBLE, null_rate DOUBLE, distinct_ratio DOUBLE, avg_strlen DOUBLE, dtype STRING")
reco_df = spark.createDataFrame(recommendations) if recommendations else spark.createDataFrame([], "dimension STRING, column STRING, approx_cardinality INT, coverage_pct DOUBLE, normalized_entropy DOUBLE, contribution_score DOUBLE, null_rate DOUBLE, distinct_ratio DOUBLE, avg_strlen DOUBLE, dtype STRING, recommendation STRING, reasons STRING")

# 1) Dimension ranking
dims_ranked = dims_df.orderBy(F.col("best_column_score").desc_nulls_last(), F.col("coverage_pct").desc())
display(dims_ranked)

# 2) Per-column scores across all dimensions (sorted by contribution)
cols_ranked = cols_df.orderBy(F.col("contribution_score").desc())
display(cols_ranked)

# 3) Recommendations (what to KEEP in core vs MOVE to aggregation/ext)
reco_ranked = (
    reco_df
    .orderBy(F.col("recommendation").asc(), F.col("dimension").asc(), F.col("contribution_score").desc())
)
display(reco_ranked)

# 4) Convenience views:
#    - Columns to KEEP in core
core_cols = reco_df.where(F.col("recommendation") == F.lit("CORE")).orderBy(F.col("contribution_score").desc())
display(core_cols)

#    - Columns recommended as AGG_CANDIDATE (move to "ext"/aggregation table)
agg_cols = reco_df.where(F.col("recommendation") == F.lit("AGG_CANDIDATE")).orderBy(F.col("contribution_score").asc())
display(agg_cols)

print("""
Interpretation:
- CORE           -> keep in the primary dimension connected to the detail table (strong slicers).
- AGG_CANDIDATE  -> move to a secondary/aggregation (ext) dimension to reduce width and scans.
Tune thresholds in the POLICY section to match model behavior and usage patterns.
""")
