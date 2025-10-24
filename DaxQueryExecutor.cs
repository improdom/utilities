Hi All,

The model has been deployed under the link below.
Performance before the Databricks table optimization was already good — most queries are now completing within a few seconds.

🔗 ARC Risk Model PoC V4.1

Several MRV calculations are currently broken due to recent table changes, and a few table relationships are not yet properly configured. I’ll fix these before promoting to QA.

@Ankush / Sridhar / Patrudu – please run some tests and focus on performance; you can ignore the calculation values for now.
If you have access to Databricks DEV, please review the SQL queries being generated. Report any issues to Marcin or Jerzy for investigation.

The model in DEV is running in DirectQuery mode, so expect slower performance compared to in-memory models.
As discussed, today our focus is on Databricks optimization rather than query speed in Power BI QA.

Thanks,
Julio Diaz
