Hi Jay,

Could you please revisit the possibility of enabling Power BI Query Scale-Out at the tenant level?

I had requested this feature last year for another project, but it was not approved due to tenant-wide governance concerns (see attached email).

As we investigate the CubIQ concurrency issues, Query Scale-Out could help reduce contention within the semantic model by distributing report requests across multiple model replicas before they reach the gateway. While it won't resolve potential gateway bottlenecks, it could improve overall query throughput and help us determine whether the semantic model is a contributing factor.

If enabling it is still not possible, I completely understand. I just wanted to see if it could be reconsidered given our current performance challenges.

Thanks,
Julio
