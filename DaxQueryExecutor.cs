from pyspark.sql.functions import col
from pyspark.sql.types import DecimalType

# Table info
database_name = "hive_metastore.mt_arc_composite_gold_views"
table_name = "pbi_data_retention_point"

# Read table
df = spark.table(f"{database_name}.{table_name}")

# List of double columns to convert
columns_to_convert = ["column1", "column2", "column3"]  # Replace with your actual column names

# Cast specified columns to DecimalType
converted_df = df.select([
    col(c).cast(DecimalType(38, 10)).alias(c) if c in columns_to_convert else col(c)
    for c in df.columns
])

# Overwrite the table (preserving partitioning if needed)
converted_df.write.format("delta") \
    .partitionBy("query_name") \  # Remove this line if your table is not partitioned
    .mode("overwrite") \
    .option("overwriteSchema", "true") \
    .saveAsTable(f"{database_name}.{table_name}")

print("Table updated with selected columns converted to decimal.")
