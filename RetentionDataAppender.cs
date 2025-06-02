Hi All,

The Strategic model has been successfully refreshed after implementing some updates in both Power BI and the Databricks model. The total model size in Power BI for one full COB (05/27/2025) is approximately 20.4 GB, which is consistent with the current model size.

Data processing is slower than usual, as expected, due to the increased data volume and memory requirements. However, the performance is better than initially anticipated. The dimension tables were refreshed in about 15 minutes after applying a single COB filter. The full fact table refresh took approximately 3 hours, using 90 partitions and a refresh parallelism level of 20.

There is still room for optimization—particularly by adjusting the partition strategy and implementing improved sorting techniques.

I also identified some Referential Integrity (RI) violations in the VertiPaq analysis. These typically occur when a foreign key in the fact table references a key that does not exist in the related dimension table. These violations should be reviewed and addressed.

Venkat, as we prepare to move this to the test lane, please verify the model post-refresh and coordinate with Abhishek on the data validation.

Best regards,
[Your Name]
