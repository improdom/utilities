from pyspark.sql import functions as F

# -------------------------
# CONFIG
# -------------------------
source_table = "arc_risk_results_detail"
target_table = "arc_risk_results_detail_v2"

# cob_date_id you ALREADY have in the detail table (the "real" one)
source_cob = 20251022

# the 8 new cob_date_id values you want to generate (must all be distinct)
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

# randomization band for report_value
# 0.20 means total band 20%, which is ±10% around the original value
RANDOM_SCALE = 0.20

# -------------------------
# 1. READ / CACHE SOURCE SLICE
#    only rows for the source_cob
# -------------------------
base_df = (
    spark.read.table(source_table)
         .filter(F.col("cob_date_id") == F.lit(source_cob))
         .cache()
)

# materialize the cache now so we don't keep rereading storage in the loop
base_df.count()

# We'll also capture the original data type of report_value
report_value_field = [f for f in base_df.schema.fields if f.name == "report_value"][0]
report_value_type_str = report_value_field.dataType.simpleString()  # e.g. 'decimal(38,10)'

# -------------------------
# 2. BUILD CLONES FOR EACH NEW COB
#    for each target cob_date_id in new_cobs:
#      - set cob_date_id = that value
#      - randomize report_value
# -------------------------
clones = []

for nc in new_cobs:
    clone_df = (
        base_df
          .withColumn("cob_date_id", F.lit(nc))
          .withColumn(
              "report_value",
              (
                  F.col("report_value")
                  * (F.lit(1) + (F.rand() - F.lit(0.5)) * F.lit(RANDOM_SCALE))
              ).cast(report_value_type_str)
          )
    )
    clones.append(clone_df)

# Union them all into one big DataFrame
final_df = clones[0]
for extra_df in clones[1:]:
    final_df = final_df.unionByName(extra_df)

# -------------------------
# 3. CREATE TARGET TABLE IF NEEDED
#    - Delta
#    - liquid clustering enabled
#    - clustering keys = cob_date_id, measure_id
#
# We infer the schema from base_df. That keeps column names and types consistent.
# -------------------------
cols_ddl = []
for field in base_df.schema.fields:
    spark_type_str = field.dataType.simpleString()
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
# 4. APPEND THE 8-COB UNION INTO THE NEW LIQUID-CLUSTERED TABLE
# -------------------------
(
    final_df
      .write
      .format("delta")
      .mode("append")
      .saveAsTable(target_table)
)

# -------------------------
# 5. (RECOMMENDED) COMPACT / RESPECT CLUSTERING
#    This step helps Databricks physically organize data on (cob_date_id, measure_id),
#    which keeps DirectQuery fast.
# -------------------------
spark.sql(f"OPTIMIZE {target_table}")
