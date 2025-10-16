This table should be split based on column cardinality or logical grouping:

Risk Factor Extension 1 – Holds low-cardinality columns (faster lookups, better cache reuse).

Risk Factor Extension 2 – Holds higher-granularity columns (slower but only queried when needed, avoiding performance impact on other columns).
