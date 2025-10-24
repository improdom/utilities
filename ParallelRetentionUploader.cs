from pyspark.sql import functions as F
from pyspark.sql import types as T

# -------------------------
# CONFIG
# -------------------------
source_table = "arc_risk_results_detail"
target_table = "arc_risk_results_detail_v2"

# This is the cob_date_id that already exists in the source table.
# Must be an int, example: 20251022
source_cob = 20251022

# These are the 8 NEW cob_date_id values you want to generate.
# All must be int. They must NOT already exist in the target table (or you'll duplicate).
new_cobs = [
    20251023,
    20251024,
    20251025,
    20251026,
    20251027,
    20251028,
    20251029,
    20251030
]

# How much to randomize report_value:
# RANDOM_SCALE = 0.20 means "multiply by (1 +/- 10%)"
RANDOM_SCALE = 0.20

# -------------------------
# 1. READ / CACHE SOURCE SLICE
#    We'll only pull rows for the one source COB we want to clone.
#    This should prune partitions in the old table.
# -------------------------
base_df = (
    spark.read.table(source_table)
         .filter(F.col("cob_date_id") == F.lit(source_cob))  # cob_date_id is int
         .cache()
)

# Materialize cache so subsequent loops don't re-read storage
base_df.count()

# Capture the original data type of report_value (ex: decimal(38,10), double, etc.)
report_value_field = [f for f in base_df.schema.fields if f.name == "report_value"][0]
report_value_type_str = report_value_field.dataType.simpleString()

# We'll also confirm cob_date_id's dtype is int and enforce int on writes
cob_type_is_int = [f for f in base_df.schema.fields if f.name == "cob_date_id"][0].dataType
# (We won't cast it here if it's already int, but we'll explicitly cast in clones just to be clean.)


# -------------------------
# 2. BUILD CLONES FOR EACH NEW COB
#    For each desired COB:
#      - overwrite cob_date_id with that int
#      - randomize report_value
# -------------------------
clones = []

for nc in new_cobs:
    clone_df = (
        base_df
          # set cob_date_id to the new int value
          .withColumn("cob_date_id", F.lit(int(nc)).cast(T.IntegerType()))
          # perturb report_value within +/- RANDOM_SCALE/2
          .withColumn(
              "report_value",
              (
                  F.col("report_value")
                  * (F.lit(1) + (F.rand() - F.lit(0.5)) * F.lit(RANDOM_SCALE))
              ).cast(report_value_type_str)
          )
    )
    clones.append(clone_df)

# Union all cloned COBs into one big DataFrame
final_df = clones[0]
for extra_df in clones[1:]:
    final_df = final_df.unionByName(extra_df)

# Safety check: enforce cob_date_id back to int just in case Spark tried to widen types
final_df = final_df.withColumn("cob_date_id", F.col("cob_date_id").cast(T.IntegerType()))

# -------------------------
# 3. CREATE TARGET TABLE IF NEEDED
#    We'll build a CREATE TABLE ... USING DELTA with liquid clustering.
#
#    We DO NOT partition BY anything here.
#    Instead we turn on liquid clustering and tell it which columns to cluster on.
#
#    Cluster keys = (cob_date_id, measure_id)
#    These should be the columns Power BI filters on most often.
# -------------------------
cols_ddl = []
for field in base_df.schema.fields:
    spark_type_str = field.dataType.simpleString()

    # Force cob_date_id to INT in the DDL for clarity.
    # Spark sometimes represents int columns as 'int' already. We'll just standardize.
    if field.name == "cob_date_id":
        spark_type_str = "int"

    cols_ddl.append(f"`{field.name}` {spark_type_str}")

create_table_ddl = f"""
CREATE TABLE IF NOT EXISTS {target_table} (
  {",\n  ".join(cols_ddl)}
)
USING DELTA
TBLPROPERTIES (
  delta.liquidClustering.enabled = true,
  delta.liquidClustering.columns = 'cob_date_id,measure_id'
)
"""

spark.sql(create_table_ddl)

# -------------------------
# 4. APPEND THE SYNTHETIC 8-COB DATA INTO THE NEW TABLE
# -------------------------
(
    final_df
      .write
      .format("delta")
      .mode("append")
      .saveAsTable(target_table)
)

# -------------------------
# 5. CLUSTER MAINTENANCE / COMPACTION
#    This step helps the liquid clustering engine keep data for the same
#    (cob_date_id, measure_id) packed together so Databricks can skip fast.
# -------------------------
spark.sql(f"OPTIMIZE {target_table}")
