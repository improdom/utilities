| Status                     | Description                                                              |
| -------------------------- | ------------------------------------------------------------------------ |
| `METADATA_COPIED`          | Metadata copied from on-prem system to staging environment.              |
| `MRV_CREATED_STAGING`      | MRV Measures created in Power BI staging model                           |
| `RECONCILIATION_PENDING`   | Awaiting reconciliation execution to verify correctness of the measures. |
| `RECONCILED`               | Reconciliation completed; measures passed automated validation.          |
| `APPROVAL_PENDING`         | Awaiting manual verification and approval by user.                       |
| `APPROVED`                 | User has verified and approved the measures.                             |
| `METADATA_PUBLISHED`       | Approved metadata copied to main environment.                            |
| `MEASURES_CREATED_MAIN`    | Final measures created in the main Power BI model.                       |
| `COMPLETED`                | Entire process successfully completed.                                   |
