Hi Jay,

Could you please revisit the possibility of enabling Power BI Query Scale-Out at the tenant level?

I had requested this feature last year for another project, but it was not approved due to tenant-wide governance concerns (see attached email).

As we investigate the CubIQ concurrency issues, Query Scale-Out could help by distributing report requests across multiple semantic model replicas, reducing contention within a single model instance and allowing more requests to be processed in parallel. Although CubIQ runs entirely in DirectQuery mode, the semantic model still performs query processing and coordination before and after sending requests to Databricks, so Query Scale-Out could still provide a meaningful benefit. It would also help us evaluate whether the single semantic model instance is a significant contributor to the concurrency issues we're experiencing.

Thanks,

Julio
