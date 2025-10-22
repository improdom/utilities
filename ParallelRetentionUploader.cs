Hi [Manager’s Name],

I’d like to request support to optimize our Databricks tables, particularly the fact tables used by the Power BI models. At the moment, the tables are partitioned by multiple columns (cob_date_id, event_run_time_id, and measure), which is leading to an excessive number of small files and subdirectories. This increases shuffle operations and impacts query performance.

To improve efficiency, I recommend simplifying the partitioning and introducing Liquid Clustering:

Partition only by cob_date_id — this enables efficient partition pruning for daily data loads without creating too many directories.

Apply Liquid Clustering by frequently filtered columns, such as measure and scenario_id. This allows Databricks to physically co-locate related rows, enabling data skipping and reducing the amount of data scanned and shuffled during queries.

Together, this approach will:

Minimize shuffle operations and disk I/O during joins and aggregations.

Improve read performance for both Databricks SQL and Power BI DirectQuery.

Maintain balanced file sizes (128–512 MB) and reduce maintenance overhead compared to multi-level partitioning.

Allow adaptive query execution to better optimize join strategies at runtime.

If you agree, I’d like to proceed with this optimization plan in our development workspace and benchmark the performance gains before rolling it out to production.

Best regards,
Julio Diaz
Market Risk – Aggregation Team
