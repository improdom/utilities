Hi Durga,

Thanks for validating the MRV partitions and confirming alignment with the Databricks metadata.

As of today, I have updated the MRV builder to reference the Databricks Delta table directly for partition creation and population.

For context, when we previously discussed sourcing the partition metadata from Databricks, the understanding was that partition IDs would remain stable. Based on that assumption, prioritization of this change was deferred at the time.

During the recent investigation, I observed that the Azure SQL metadata tables were not fully synchronized with the Databricks partitions, which contributed to the discrepancy seen. With the update now in place, the MRV workflow will consistently rely on the Databricks Delta table as the single source of truth going forward.

Happy to review the changes together or discuss any follow-ups if needed.

Thanks,
Julio
