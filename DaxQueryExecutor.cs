# Databricks notebook cell (PySpark)
# Smart table optimizer: analyze -> decide -> (optionally) optimize
from pyspark.sql import functions as F, types as T
from datetime import datetime, timedelta

# --- Tunables (adjust to your environment) ---
TARGET_FILE_MB            = 128               # Databricks aims ~128MB effective; <32MB avg often means compaction helps
SMALL_FILE_AVG_MB_CUTOFF  = 32
SMALL_FILE_MIN_COUNT      = 500
MIN_TABLE_SIZE_FOR_ZORDER = 10 * 1024**3      # 10 GiB
MAX_ZORDER_COLS           = 3
CARD_MIN                  = 50                 # ignore tiny-cardinality cols for ZORDER
CARD_MAX                  = 50_000_000        # ignore near-unique huge-cardinality cols for ZORDER
NULL_FRAC_MAX             = 0.5
SKEW_RATIO_ALERT          = 5.0               # max/min (or p95/p50) partition row-count skew
HEAVY_WRITE_WINDOW_DAYS   = 7
HEAVY_WRITE_COMMITS_MIN   = 50
STALE_OPTIMIZE_DAYS       = 14
SAMPLE_FRACTION           = 0.01              # sample for cardinality/null-rate; capped by SAMPLE_MAX_ROWS
SAMPLE_MAX_ROWS           = 10_000_000

# --- Helpers ---
def _fq_name(catalog_db_table: str):
    parts = [p for p in catalog_db_table.split('.') if p]
    if len(parts) == 3:
        return catalog_db_table
    elif len(parts) == 2:
        return f"`{parts[0]}`.`{parts[1]}`"
    elif len(parts) == 1:
        return f"`{parts[0]}`"
    else:
        raise ValueError("Invalid table name")

def describe_detail(fq):
    return spark.sql(f"DESCRIBE DETAIL {fq}").first().asDict()

def describe_history(fq, limit=5000):
    return spark.sql(f"DESCRIBE HISTORY {fq}").limit(limit)

def get_table_schema(fq):
    return spark.table(fq).schema

def get_partition_cols(fq):
    ext = spark.sql(f"DESCRIBE EXTENDED {fq}").where("col_name='partitionColumns'").select("data_type").first()
    if not ext or not ext[0]:
        return []
    raw = ext[0].strip()
    # looks like: ["colA","colB"]
    return [c.strip().strip('"`') for c in raw.strip('[]').split(',')] if raw != "[] " else []

def count_partitions_and_skew(fq, part_cols):
    if not part_cols:
        return {"has_partitions": False}
    cols = ", ".join([f"`{c}`" for c in part_cols])
    df = spark.sql(f"SELECT {cols}, COUNT(*) as cnt FROM {fq} GROUP BY {cols}")
    stats = df.agg(
        F.count("*").alias("num_parts"),
        F.min("cnt").alias("min_cnt"),
        F.expr("percentile(cnt, array(0.50,0.95))").alias("pct")
    ).first()
    if stats is None:
        return {"has_partitions": True, "num_parts": 0}
    pct50, pct95 = stats["pct"][0], stats["pct"][1]
    skew_ratio = (pct95 / max(pct50,1)) if pct50 else None
    return {
        "has_partitions": True,
        "num_parts": stats["num_parts"],
        "min_cnt": stats["min_cnt"],
        "p50_cnt": pct50,
        "p95_cnt": pct95,
        "skew_ratio": skew_ratio
    }

def sample_df_for_stats(df):
    # sample bounded by SAMPLE_MAX_ROWS
    total = df.count()
    if total == 0:
        return df.limit(0), 0
    frac = min(1.0, max(SAMPLE_FRACTION, min(1.0, SAMPLE_MAX_ROWS / total)))
    sdf = df.sample(withReplacement=False, fraction=frac, seed=42)
    return sdf, total

def zorder_candidates(fq, schema, part_cols):
    # Prefer non-partition columns used for filters: date/timestamp/numeric/string, with sane cardinality & nulls
    df = spark.table(fq)
    sdf, total = sample_df_for_stats(df)
    if total == 0:
        return []

    cols = []
    for f in schema.fields:
        name = f.name
        if name in part_cols:
            continue
        dt = f.dataType
        is_supported = isinstance(dt, (T.DateType, T.TimestampType, T.StringType, T.IntegerType, T.LongType, T.ShortType, T.FloatType, T.DoubleType, T.DecimalType))
        if not is_supported:
            continue
        cols.append(name)

    if not cols:
        return []

    aggs = []
    for c in cols:
        aggs.append(F.approx_count_distinct(F.col(c)).alias(f"{c}__card"))
        aggs.append((F.sum(F.when(F.col(c).isNull(), 1).otherwise(0)) / F.count(F.lit(1))).alias(f"{c}__nullfrac"))
    row = sdf.agg(*aggs).first().asDict()

    scored = []
    for c in cols:
        card = row.get(f"{c}__card") or 0
        nullf = float(row.get(f"{c}__nullfrac") or 0.0)
        if card < CARD_MIN or card > CARD_MAX:
            continue
        if nullf > NULL_FRAC_MAX:
            continue
        # score: prefer timestamp/date, then moderate-high cardinality, then lower nulls
        dtype = [f.dataType for f in schema.fields if f.name==c][0]
        type_boost = 2.0 if isinstance(dtype,(T.DateType, T.TimestampType)) else 1.0
        score = type_boost * (min(card, CARD_MAX)/CARD_MAX) * (1.0 - nullf)
        scored.append((c, score, int(card), nullf, str(dtype)))

    scored.sort(key=lambda x: x[1], reverse=True)
    return [c for c,_,_,_,_ in scored[:MAX_ZORDER_COLS]], scored

def last_optimized_at(hist_df):
    # Look for OPTIMIZE in operation column
    opt = hist_df.where(F.col("operation")=="OPTIMIZE").orderBy(F.col("timestamp").desc()).select("timestamp").limit(1).collect()
    return opt[0][0] if opt else None

def recent_write_churn(hist_df, window_days):
    since = datetime.utcnow() - timedelta(days=window_days)
    w = hist_df.where(F.col("timestamp") >= F.lit(since)).where(F.col("operation").isin("WRITE","MERGE","DELETE","UPDATE","TRUNCATE"))
    return w.count()

def liquid_clustering_enabled(fq):
    props = spark.sql(f"SHOW TBLPROPERTIES {fq}").where("key like 'delta.feature.liquidClustering%' OR key like 'delta.liquid.%'")
    kv = {r['key']: r['value'] for r in props.collect()}
    enabled = kv.get('delta.feature.liquidClustering','').lower() in ('enabled','supported','true')
    cols = kv.get('delta.liquid.columns') or kv.get('delta.liquid.clustering.columns')
    return enabled, cols

def analyze_table(full_name: str, execute: bool=False):
    fq = _fq_name(full_name)
    detail = describe_detail(fq)
    hist   = describe_history(fq)
    schema = get_table_schema(fq)
    part_cols = get_partition_cols(fq)
    part_stats = count_partitions_and_skew(fq, part_cols)

    size_bytes = int(detail['sizeInBytes'])
    num_files  = int(detail['numFiles'])
    loc        = detail['location']
    format     = detail['format']
    tbl_name   = detail.get('name', full_name)

    avg_file_mb = (size_bytes/1_048_576) / max(num_files,1)
    size_gb = size_bytes / (1024**3)

    last_opt = last_optimized_at(hist)
    churn = recent_write_churn(hist, HEAVY_WRITE_WINDOW_DAYS)
    liquid_enabled, liquid_cols = liquid_clustering_enabled(fq)

    # Decide ZORDER candidates (only if not liquid-clustered)
    zc_list, zc_scored = ([], [])
    if not liquid_enabled:
        zc_list, zc_scored = zorder_candidates(fq, schema, part_cols)

    reasons = []
    actions = []

    # Reason 1: small files
    if (avg_file_mb < SMALL_FILE_AVG_MB_CUTOFF) and (num_files >= SMALL_FILE_MIN_COUNT):
        reasons.append(f"Too many small files: avg_file_size={avg_file_mb:.1f} MB across {num_files} files (target ~{TARGET_FILE_MB} MB).")
        if not liquid_enabled:
            if zc_list:
                actions.append(f"OPTIMIZE {fq} ZORDER BY ({', '.join([f'`{c}`' for c in zc_list])});")
            else:
                actions.append(f"OPTIMIZE {fq};")
        else:
            reasons.append("Liquid clustering is enabled; compaction happens via clustering jobs instead of OPTIMIZE.")

    # Reason 2: partition skew
    if part_stats.get("has_partitions"):
        num_parts = part_stats.get("num_parts", 0)
        skew = part_stats.get("skew_ratio")
        if num_parts and skew and skew >= SKEW_RATIO_ALERT:
            reasons.append(f"Partition skew detected on ({', '.join(part_cols)}): p95/p50 row-count ratio ≈ {skew:.2f} over {num_parts} partitions.")
            if not liquid_enabled:
                if zc_list:
                    actions.append(f"-- Mitigate skew via file clustering and skipping")
                    actions.append(f"OPTIMIZE {fq} ZORDER BY ({', '.join([f'`{c}`' for c in zc_list])});")

    # Reason 3: large table and not recently optimized
    if (size_gb >= MIN_TABLE_SIZE_FOR_ZORDER/(1024**3)) and (not liquid_enabled):
        stale = (last_opt is None) or ((datetime.utcnow() - last_opt.replace(tzinfo=None)) > timedelta(days=STALE_OPTIMIZE_DAYS))
        if stale and zc_list:
            when_txt = "never" if last_opt is None else f"on {last_opt.strftime('%Y-%m-%d')}"
            reasons.append(f"Large table (~{size_gb:.1f} GiB) with no recent OPTIMIZE (last {when_txt}); Z-ORDER on filterable columns should improve skipping.")
            actions.append(f"OPTIMIZE {fq} ZORDER BY ({', '.join([f'`{c}`' for c in zc_list])});")

    # Reason 4: heavy recent churn
    if churn >= HEAVY_WRITE_COMMITS_MIN and not liquid_enabled:
        reasons.append(f"Heavy write churn in last {HEAVY_WRITE_WINDOW_DAYS} days: {churn} commits; compaction can reduce file碎化 (fragmentation).")
        if zc_list:
            actions.append(f"OPTIMIZE {fq} ZORDER BY ({', '.join([f'`{c}`' for c in zc_list])});")
        else:
            actions.append(f"OPTIMIZE {fq};")

    # If liquid clustering enabled, suggest verifying clustering progress
    if liquid_enabled:
        reasons.append("Liquid Clustering detected. Ensure clustering maintenance is active and target file sizes are healthy.")

    # Consolidate: unique actions preserving order
    seen = set()
    unique_actions = []
    for a in actions:
        if a not in seen:
            unique_actions.append(a)
            seen.add(a)

    # Build report
    print("=== Smart Optimization Report ===")
    print(f"Table          : {tbl_name}")
    print(f"Location       : {loc}")
    print(f"Format         : {format}")
    print(f"Size           : {size_gb:.2f} GiB")
    print(f"Files          : {num_files} (avg ~{avg_file_mb:.1f} MB/file)")
    print(f"Partitions     : {part_cols if part_cols else 'None'}")
    if part_stats.get("has_partitions"):
        print(f"  Partitions   : {part_stats.get('num_parts')}")
        if part_stats.get("skew_ratio") is not None:
            print(f"  Skew p95/p50 : {part_stats.get('skew_ratio'):.2f}")
    print(f"LiquidCluster  : {liquid_enabled} {('cols='+str(liquid_cols)) if liquid_cols else ''}")
    print(f"Last OPTIMIZE  : {last_opt if last_opt else 'never'}")
    print(f"Recent writes  : {churn} commits in {HEAVY_WRITE_WINDOW_DAYS} days")
    if zc_scored:
        print("Z-ORDER candidates (top):")
        for c,score,card,nullf,dt in zc_scored[:MAX_ZORDER_COLS]:
            print(f"  - {c:>24s} | score={score:0.3f} | approx_card={card:,} | null%={nullf*100:0.1f} | {dt}")

    print("\nNeeds optimization?:", "YES" if reasons else "NO")
    if reasons:
        print("\nReasons:")
        for r in reasons:
            print(f" - {r}")

    if unique_actions and not liquid_enabled:
        print("\nPlanned SQL:")
        for a in unique_actions:
            print(f" {a}")

    # Execute if asked (and if not liquid clustered)
    if execute and unique_actions and not liquid_enabled:
        for a in unique_actions:
            print(f"\n-- Executing: {a}")
            spark.sql(a)
        print("\nDone. Consider VACUUM separately if appropriate; do not reduce retention below 7 days unless you fully understand the risk.")
    elif execute and liquid_enabled:
        print("\nExecution skipped: table uses Liquid Clustering; manage via clustering settings/jobs instead of OPTIMIZE.")

# ---------------------------
# HOW TO USE:
# analyze only (safe):
# analyze_table("catalog.schema.table", execute=False)
#
# analyze and optimize when thresholds are breached:
# analyze_table("catalog.schema.table", execute=True)
# ---------------------------
