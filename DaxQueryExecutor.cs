Subject: RE: ARC PREPROD ENV: SQL Warehouse Cluster Auto Shutdown Delay

Hi Nagesh,

As discussed, setting a shutdown window (even 1 hour or more) is risky, as users may connect to the model at any time. To ensure availability, the cluster will need to run continuously.

During our call last week, we also agreed to upgrade the SQL Warehouse cluster to Large size to improve DirectQuery performance for end users. Could you please look into this as well?

I’m currently running some validation tests in Excel before delivering the model to users. Please let me know once the changes are complete so I can confirm from Power BI.

Thanks,
Julio Diaz
