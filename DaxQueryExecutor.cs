from pyspark.sql.functions import col

# Table and database info
database_name = "mt_arc_composite_gold_views"
table_name = "pbi_data_retention_point"

# Read the original Delta table
df = spark.table(f"{database_name}.{table_name}")

# Rename columns containing '__' to '_'
renamed_df = df.select([
    col(c).alias(c.replace("__", "_")) if "__" in c else col(c)
    for c in df.columns
])

# Backup original table (optional but recommended)
backup_table_name = f"{table_name}_backup"
spark.sql(f"CREATE TABLE IF NOT EXISTS {database_name}.{backup_table_name} AS SELECT * FROM {database_name}.{table_name}")

# Overwrite the table with renamed columns
renamed_df.write.format("delta") \
    .mode("overwrite") \
    .option("overwriteSchema", "true") \
    .saveAsTable(f"{database_name}.{table_name}")

print("Column renaming completed successfully.")
