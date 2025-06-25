from pyspark.sql.functions import col
from pyspark.sql.types import DoubleType, DecimalType

# Table info
database_name = "hive_metastore.mt_arc_composite_gold_views"
table_name = "pbi_data_retention_point"

# Read the original Delta table
df = spark.table(f"{database_name}.{table_name}")

# Detect and convert double columns to decimal
converted_df = df.select([
    col(c).cast(DecimalType(38, 10)).alias(c) if f.dataType == DoubleType() else col(c)
    for c, f in df.schema.fields
])

# Overwrite the table (preserving partitioning if needed)
converted_df.write.format("delta") \
    .partitionBy("query_name") \  # <-- Remove this line if table is not partitioned
    .mode("overwrite") \
    .option("overwriteSchema", "true") \
    .saveAsTable(f"{database_name}.{table_name}")

print("Double columns converted to DecimalType(38,10) and table overwritten successfully.")
