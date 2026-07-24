Subject: ADBC Migration Update – Validation Request

Hi Jaya,

I wanted to share a quick update on the Databricks ADBC migration.

I updated the semantic model to use the ADBC connector by setting Implementation="2.0" for all Databricks connections. When running the model locally in Power BI Desktop, I was able to verify that it is using ADBC:

* Databricks Query History identifies the driver as ADBCDatabricksDriver (instead of the Spark ODBC driver).
* The previous Databricks warning indicating ODBC usage is no longer displayed.

I have published a test semantic model named Cubiq_ADBC, which is running in DirectQuery mode.

Could you please point the ERS report to this model and validate its behavior? In particular:

* Verify in Databricks Query History that report queries are using ADBC (the source should show ADBCDatabricksDriver).
* Let me know if you observe any performance differences or functional issues compared to the current model.Hi Anupam,

I was thinking about another approach that could help preserve self-service changes while still allowing reports to be centrally managed.

Instead of generating the report entirely from metadata, we could use the current PBIR definition downloaded from the workspace as the starting point. Self-Service would expose only the relevant report structure (attributes and filters) to the user, and then patch those changes back into the original PBIR definition while preserving visuals, bookmarks, IDs, formatting, and all other report components.

The high-level flow would be:

Download the current PBIR definition from the workspace.
Keep the original PBIR JSON as the base.
Parse and extract only the attributes and filters that the Self-Service designer needs to display, together with any additional saved metadata.
Allow the user to modify those elements.
Patch the changes back into the original PBIR JSON.
Preserve all other report components unchanged.
Validate the updated report and redeploy it. As part of the validation, verify that all attributes required by existing visuals are still present. If any required attributes have been removed, notify the user of the potential breaking changes before deployment.

This approach is technically feasible, but it would require enhancing Self-Service to support PBIR round-tripping instead of generating the report entirely from metadata.

I think this would provide a cleaner way to preserve self-service changes while minimizing the risk of overwriting existing report customizations.

Thanks,
Julio
