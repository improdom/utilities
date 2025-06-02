Hi All,

The Strategic model has been successfully refreshed following recent updates in both Power BI and the Databricks model. The total model size in Power BI for one full COB (05/27/2025) is approximately 20.4 GB, which is in line with the current production model.

Initial results are encouraging. As expected, processing time has increased due to the larger data volume and memory usage, but overall performance remains within acceptable bounds. The dimension tables completed refresh in approximately 15 minutes after filtering for a single COB. The full fact table refresh took about 3 hours, using 90 partitions and a refresh parallelism level of 20.

Please note that these are preliminary results, as I observed several Referential Integrity (RI) violations in the VertiPaq analysis. These typically occur when a foreign key in the fact table references a key that does not exist in the corresponding dimension table. While these RI issues should be addressed to ensure data quality and optimal join behavior, I do not expect them to negatively impact the current refresh performance.

Venkat, in preparation for moving to the test lane, please verify the model post-refresh and work with Abhishek on completing the data validation and addressing the RI findings.

Best regards,
[Your Name]
