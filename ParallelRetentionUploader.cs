Hi QA Team,

The error preventing execution of the stress tool was caused by the deletion of the Postgres database during the recent upgrade from version 16 to 17. Although upgrade notifications were sent, they did not indicate that databases from older or decommissioned teams would be removed.

After discussing with DevOps, restoration of the original database is not feasible at this stage. To resolve the issue, the application has been migrated to SQL Managed Instance and is now fully functional.

The tables have been recreated in **appdev** under the **marvel** database with the following names:

* cubiq.st_arc_mrv_recon_target_data
* cubiq.st_benchmark
* cubiq.st_parameters
* cubiq.st_query_runtime
* cubiq.st_query_template

Please find attached the updated installation package configured to use SQL MI tables. Unfortunately, existing benchmark data could not be recovered, so benchmarks will need to be recreated before running the MRV reconciliation queries.

Let me know if any support is needed during the setup.

Best regards,
Julio
