# Databricks / PySpark cell

# ---- config ----
TABLE = "your_db.your_delta_table"   # e.g., "mr_arc_dm_gold.pbi_business_date_control"
START_INDEX = 1                      # first suffix number (a1, a2, ...)
HASH_FUNC = "md5"                    # or "sha2" with 256, etc.
SKIP_COLS = {"_rescued_data"}        # columns to skip (case-insensitive)
# ----------------

def quote_ident(name: str) -> str:
    # backtick-quote to handle spaces/reserved words
    return f"`{name}`"

# Load schema
df = spark.table(TABLE)
cols = [f.name for f in df.schema.fields]

# Filter/normalize skip list
skip = {c.lower() for c in SKIP_COLS}
cols = [c for c in cols if c.lower() not in skip]

# Build the NVL(...) || ' a{n}' pieces
pieces = [f"NVL({quote_ident(c)}, '')||' a{idx}'"
          for idx, c in enumerate(cols, START_INDEX)]

# Compose the multi-line snippet (like your screenshot)
snippet = " ||\n".join(pieces)

# Full SELECT that computes a hash over the concatenated string
select_sql = f"""SELECT {HASH_FUNC}(
{snippet}
) AS hashString
FROM {TABLE};"""

print("-- NVL/concat snippet --")
print(snippet)
print("\n-- Full SELECT --")
print(select_sql)
