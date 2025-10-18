# Databricks / PySpark cell

# ---- config ----
TABLE = "your_db.your_delta_table"   # e.g. "mr_arc_dm_gold.pbi_business_date_control"
START_INDEX = 1                      # first suffix number (a1, a2, ...)
HASH_FUNC = "md5"                    # or "sha2" for sha2(concat, 256)
SKIP_COLS = {"_rescued_data"}        # optional: columns to skip
# ----------------

def quote_ident(name: str) -> str:
    # backtick-quote to handle special characters or reserved words
    return f"`{name}`"

# Load schema from the Delta table
df = spark.table(TABLE)
cols = [f.name for f in df.schema.fields if f.name.lower() not in {c.lower() for c in SKIP_COLS}]

# Build the NVL(...) || ' a{n}' sequence
concat_expr = " ||\n".join([f"NVL({quote_ident(c)}, '')||' a{idx}'" for idx, c in enumerate(cols, START_INDEX)])

# Build the complete SELECT statement
select_sql = f"""
SELECT
    {', '.join([quote_ident(c) for c in cols])},
    {HASH_FUNC}(
{concat_expr}
    ) AS hashString
FROM {TABLE};
"""

print("-- Generated SQL --")
print(select_sql)
