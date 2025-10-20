Marcin,
If the count in the source matches the count in the model table, then the data is being loaded at the same level of granularity as the source — meaning it’s not being cleansed or pre-aggregated before loading into the Power BI model. That’s not right.

In BI design, Power BI should never directly consume raw transactional data. It will always perform aggregations (like SUM) at query time, which is far more expensive. The data must be pre-aggregated during the ETL step so that Power BI works with summarized datasets, not granular records.

This has been a fundamental issue since the move to SDM: data is being loaded at a transaction level instead of an analytical level. That multiplies row counts unnecessarily and drives up both cost and latency — and it’s been surprisingly hard to get this point across.




  Marcin, that means we’re loading the data at the same level of granularity as the source, without proper cleansing or pre-aggregation before reaching the Power BI model. That’s not correct — Power BI should never handle raw transactional data, as it will always aggregate at query time, which is much more expensive. The data must be pre-aggregated in ETL to avoid blowing up row counts and performance costs. This has been a key issue since the move to SDM.
