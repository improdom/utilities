Hi Satya,

I didn’t manage to connect with Ramesh, but I believe he was referring to scenarios where populating a filter list is slow when using a DirectQuery attribute. In such cases, the SQL query executed against the detail table retrieves distinct values without any filter, which results in scanning the entire table and slows down performance. This is consistent with the DQ issue (case 2) we raised with Microsoft.

As we discussed last week, one option is to create an intermediate aggregation table containing a second level of attributes. This would significantly improve performance because of the smaller table size.

Another approach could be moving these attributes into a separate table running in DirectQuery mode. The drawback is that it may require additional joins, which could affect query performance.

Let’s review this in tomorrow’s call.

Hi Amar/All,

Since this behavior is already known to Microsoft and considered as working as designed, and given that even if Microsoft were to make a change it would likely take significant time, we need to continue testing the model with more advanced scenarios. We will run benchmarks that include complex calculations with MEVs, combining in-memory and DirectQuery attributes, all executed in DirectQuery mode in Databricks. This will help validate the DQ solution and reduce the risk of encountering the same issue in the future.

I will work with Abhishek to identify the most complex queries Marvel is currently using and will incorporate additional DAX into the benchmarks. If these benchmarks are successful, they will provide a stronger level of confidence for future scenarios.

Thanks,
Julio on
