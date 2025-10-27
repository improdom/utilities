We typically have around 135 event_run_time_id values per day, and about 90% of those are inactive (IS_ACTIVE = False). Partitioning or clustering by this column would create a large number of small data segments that do not align with how Power BI queries the data, since queries filter by IS_ACTIVE = True, cob_date_id, and measure_id—not by specific event IDs.

As a result, Databricks would gain little or no benefit from pruning or skipping data, while we’d incur extra overhead in managing thousands of partitions or fragmented clusters.
