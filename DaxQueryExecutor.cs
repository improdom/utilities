Hi Farhan,

Thanks for the update.

We’ll proceed with setting up the projects and deployment pipelines.

We will also need to create new dedicated service accounts for the different environments, as listed below:

Environment	Service Account Name
APP-DEV	SVC_DEV_MARVEL_CUBIQ
TEST	SVC_TEST_MARVEL_CUBIQ
PRE-PROD	SVC_PREPROD_MARVEL_CUBIQ
PROD	SVC_PROD_MARVEL_CUBIQ

Once created, these accounts will require access to the necessary systems (Power BI Nodes and Gateways, Databricks, SQL MI, etc.). We can begin provisioning access in parallel while the dependent components are being established.

Please ensure the service account names follow the standard naming convention.

Thanks,
Julio Díaz
