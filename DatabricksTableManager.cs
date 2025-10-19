# Databricks / PySpark
from pyspark.sql import functions as F
import math, re

# =========================
# CONFIG
# =========================
fact_table = "pbi_synpoc2.fact_risk_results_v2"

# Each entry: dimension table, fact FK, dimension PK
dimensions = [
    {"name": "pbi_synpoc2.dim_position", "fact_fk": "pbi_pos_id",  "dim_key": "pbi_pos_id"},
    {"name": "pbi_synpoc2.dim_product",  "fact_fk": "pbi_prod_id", "dim_key": "pbi_prod_id"},
    # add more…
]

# Columns to ignore (keys/technical/audit)
EXCLUDE_RE = re.compile(r"(?i)^(row_.*|.*_id|.*_key|hash.*|create.*|update.*|insert.*|_.*)$")

# ---- Performance knobs ----
SAMPLE_TARGET_ROWS = 5_000_000     # aim to sample ~5M fact rows; tune to your cluster
MAX_CARD_FOR_SCORING = 5000        # skip columns above this (near-unique, bad slicers)
BROADCAST_DIM_KEYS = True          # broadcast just the PK list for fast coverage calc

# ---- Policy thresholds (same spirit as before; tune to your needs) ----
CORE_MIN_CONTRIB        = 0.35
CORE_MIN_COVERAGE       = 0.70
CORE_MAX_DISTINCT_RATIO = 0.50
CORE_MAX_NULL_RATE      = 0.40

AGG_MAX_CONTRIB         = 0.15
AGG_MIN_NULL_RATE       = 0.30
AGG_MIN_DISTINCT_RATIO  = 0.10

# =========================
# SPARK SETTINGS (optional)
# =========================
spark.conf.set("spark.sql.adaptive.enabled", "true")
spark.conf.set("spark.sql.shuffle.partitions", "200")  # adjust to cluster size

# =========================
# LOAD & SAMPLE FACT
# =========================
fact = spark.table(fact_table).cache()
total_fact_rows = fact.count()

sample_frac = min(1.0, SAMPLE_TARGET_ROWS / total_fact_rows) if total_fact_rows else 1.0
fact_s = fact.sample(False, sample_frac, seed=42).cache()
sampled_fact_rows = fact_s.count()
if sampled_fact_rows == 0:
    raise ValueError("Fact sample is empty; increase SAMPLE_TARGET_ROWS.")

def log2(x: float) -> float:
    return math.log(x, 2) if x and x > 0 else 0.0

def log2_col(c):
    return F.log(c) / F.log(F.lit(2.0))

dimension_scores = []
column_recos = []

for dim in dimensions:
    dim_name = dim["name"]; fk = dim["fact_fk"]; dk = dim["dim_key"]
    dim_df = spark.table(dim_name)

    # Candidate columns = non-key, not excluded
    candidate_cols = [c for c in dim_df.columns if c.lower() != dk.lower() and not EXCLUDE_RE.match(c)]
    if not candidate_cols:
        continue

    # ---- Coverage (fast path): left-semi on keys ONLY, using broadcast of PK list ----
    keys_df = dim_df.select(dk).distinct()
    if BROADCAST_DIM_KEYS:
        from pyspark.sql.functions import broadcast
        keys_df = broadcast(keys_df)

    matched_keys = fact_s.select(fk).join(keys_df, on=(fact_s[fk] == keys_df[dk]), how="left_semi")
    matched_sample_rows = matched_keys.count()
    coverage_pct = matched_sample_rows / sampled_fact_rows if sampled_fact_rows else 0.0

    # ---- Join the sample to dimension attributes (only columns we need) with aliases (avoid ambiguity) ----
    dim_key_alias = f"__{dk}_dim"
    dim_col_alias = {c: f"__d_{c}" for c in candidate_cols}
    dim_sel = [F.col(dk).alias(dim_key_alias)] + [F.col(c).alias(dim_col_alias[c]) for c in candidate_cols]

    f = fact_s.alias("f")
    d = dim_df.select(*dim_sel).alias("d")
    joined = f.join(d, F.col(f"f.{fk}") == F.col(f"d.{dim_key_alias}"), "left").where(F.col(dim_key_alias).isNotNull()).cache()
    joined_rows = matched_sample_rows  # same as coverage numerator

    # If nothing matched in the sample, skip this dimension
    if joined_rows == 0:
        dimension_scores.append({"dimension": dim_name, "coverage_pct": 0.0, "best_column": None, "best_score": 0.0})
        continue

    # ---- Score each column with CHEAP metrics (no full groupBy entropy) ----
    # We use:
    #   approx_card = approx_count_distinct
    #   distinct_ratio = approx_card / joined_rows
    #   null_rate
    #   segmentation_proxy = log2(approx_card) / log2(min(MAX_CARD_FOR_SCORING, joined_rows))
    #   contribution = coverage_pct * segmentation_proxy
    col_entries = []

    denom_for_entropy = log2(min(MAX_CARD_FOR_SCORING, joined_rows))
    if denom_for_entropy == 0: denom_for_entropy = 1.0  # safety

    for c in candidate_cols:
        ca = dim_col_alias[c]

        approx_card = joined.agg(F.approx_count_distinct(F.col(ca)).alias("card")).first()["card"]
        if not approx_card or approx_card <= 1 or approx_card > MAX_CARD_FOR_SCORING:
            # skip near-unique or useless for slicing
            continue

        null_rate = joined.select(F.count(F.when(F.col(ca).isNull(), 1)).alias("n")).first()["n"] / joined_rows
        distinct_ratio = approx_card / joined_rows

        segmentation_proxy = log2(approx_card) / denom_for_entropy  # 0..1-ish
        segmentation_proxy = float(max(0.0, min(1.0, segmentation_proxy)))

        contribution = float(coverage_pct) * float(segmentation_proxy)

        # Classify
        is_core = (
            (contribution >= CORE_MIN_CONTRIB) and
            (coverage_pct >= CORE_MIN_COVERAGE) and
            (distinct_ratio <= CORE_MAX_DISTINCT_RATIO) and
            (null_rate <= CORE_MAX_NULL_RATE)
        )
        is_agg = (
            (contribution < AGG_MAX_CONTRIB) or
            (null_rate >= AGG_MIN_NULL_RATE) or
            (distinct_ratio <= AGG_MIN_DISTINCT_RATIO)
        )
        label = "CORE" if (is_core and not is_agg) else ("AGG_CANDIDATE" if (is_agg and not is_core) else "NEUTRAL")

        col_entries.append({
            "dimension": dim_name,
            "column": c,
            "approx_cardinality": int(approx_card),
            "coverage_pct": float(coverage_pct),
            "segmentation_proxy": float(segmentation_proxy),
            "contribution_score": float(contribution),
            "null_rate": float(null_rate),
            "distinct_ratio": float(distinct_ratio),
            "recommendation": label
        })

    # Store per-dimension summary
    if col_entries:
        best = max(col_entries, key=lambda x: x["contribution_score"])
        dimension_scores.append({"dimension": dim_name, "coverage_pct": float(coverage_pct),
                                 "best_column": best["column"], "best_score": float(best["contribution_score"])})
        column_recos.extend(col_entries)
    else:
        dimension_scores.append({"dimension": dim_name, "coverage_pct": float(coverage_pct),
                                 "best_column": None, "best_score": 0.0})

# =========================
# OUTPUTS
# =========================
dim_df = spark.createDataFrame(dimension_scores)
col_df = spark.createDataFrame(column_recos) if column_recos else spark.createDataFrame([], "dimension STRING, column STRING, approx_cardinality INT, coverage_pct DOUBLE, segmentation_proxy DOUBLE, contribution_score DOUBLE, null_rate DOUBLE, distinct_ratio DOUBLE, recommendation STRING")

display(dim_df.orderBy(F.col("best_score").desc()))
display(col_df.orderBy(F.col("contribution_score").desc()))

agg_cols = col_df.where(F.col("recommendation") == F.lit("AGG_CANDIDATE")).orderBy(F.col("contribution_score").asc())
core_cols = col_df.where(F.col("recommendation") == F.lit("CORE")).orderBy(F.col("contribution_score").desc())
display(agg_cols)
display(core_cols)

print(f"Sampled {sampled_fact_rows:,} / {total_fact_rows:,} fact rows (fraction ~{sample_frac:.4f}).")
print("""
Interpretation:
- contribution_score ≈ coverage × segmentation_proxy, where segmentation_proxy uses log-cardinality (no heavy shuffles).
- CORE → keep in primary dim (DQ path). AGG_CANDIDATE → move to ext/aggregation dim (Import agg path).
- Raise SAMPLE_TARGET_ROWS or lower MAX_CARD_FOR_SCORING if you want finer ranking; results should stabilize quickly.
""")
