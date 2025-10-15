# ---------- config ----------
table_fullname = "your_db.your_table"       # <- change me
key_col        = "pbi_rf1_id"               # <- required key
use_approx     = True                       # set False to use exact COUNT(DISTINCT)
threshold      = 200                        # split point
ext1_name      = f"{table_fullname.split('.')[-1]}_ext1"
ext2_name      = f"{table_fullname.split('.')[-1]}_ext2"
target_db      = table_fullname.split('.')[0]

# ---------- helpers ----------
from pyspark.sql import functions as F

# Get schema & keep only simple/atomic columns
df = spark.table(table_fullname)
atomic_types = {"string","boolean","byte","short","integer","long","float","double","date","timestamp","decimal"}
cols = [(name, dtype) for name, dtype in df.dtypes if dtype.split('(')[0].lower() in atomic_types]

# Ensure key exists
col_names = [c for c,_ in cols]
if key_col not in col_names:
    raise ValueError(f"Key column `{key_col}` not found in {table_fullname}.")

# Build expressions for distinct counts
def card_expr(c):
    return (F.approx_count_distinct(F.col(c)).alias(c) if use_approx
            else F.countDistinct(F.col(c)).alias(c))

# Compute cardinalities in one pass (wide row with one metric per col), then melt to long form
card_row = df.agg(*[card_expr(c) for c,_ in cols]).collect()[0].asDict()
card = [(c, int(card_row[c])) for c,_ in cols]

# Split columns
low_cols  = [c for c,_ in cols if card_row[c] <  threshold and c != key_col]
high_cols = [c for c,_ in cols if card_row[c] >= threshold and c != key_col]

# Always include key in both
ext1_cols = [key_col] + low_cols
ext2_cols = [key_col] + high_cols

# Safety: avoid empty select lists (beyond the key)
if len(ext1_cols) == 1:
    print("Note: ext1 only contains the key (no cols < threshold).")
if len(ext2_cols) == 1:
    print("Note: ext2 only contains the key (no cols ≥ threshold).")

# Report
print("Cardinality summary (column -> count):")
for c, cnt in sorted(card, key=lambda x: x[1]):
    print(f"{c:40s} {cnt}")

print("\next1 columns (< threshold):", ext1_cols)
print("ext2 columns (>= threshold):", ext2_cols)

# Create the output tables in the same database
spark.sql(f"USE {target_db}")

cols_to_sql = lambda L: ", ".join([f"`{c}`" for c in L])

spark.sql(f"""
CREATE OR REPLACE TABLE `{target_db}`.`{ext1_name}` AS
SELECT {cols_to_sql(ext1_cols)}
FROM {table_fullname}
""")

spark.sql(f"""
CREATE OR REPLACE TABLE `{target_db}`.`{ext2_name}` AS
SELECT {cols_to_sql(ext2_cols)}
FROM {table_fullname}
""")

print(f"\nCreated tables: {target_db}.{ext1_name} and {target_db}.{ext2_name}")
