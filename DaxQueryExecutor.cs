table = "your_database.your_table"

# Get all columns in the table
cols = [r['col_name'] for r in spark.sql(f"DESCRIBE {table}").collect()]

results = []
for c in cols:
    cnt = spark.sql(f"SELECT COUNT(DISTINCT `{c}`) AS cardinality FROM {table}").collect()[0][0]
    results.append((c, cnt))

# Convert to a DataFrame and sort
df = spark.createDataFrame(results, ["column_name", "cardinality"])
df.orderBy("cardinality").show(truncate=False)
