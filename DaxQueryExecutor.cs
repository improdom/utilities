Hi all,

As discussed, the MRV metadata is now available in the Marvel database in SQL MI, under the CubIQ schema.

Since this is a shared/common database, there is no longer a need for a separate API to serve this data. Attached is a SQL query that produces a dataset equivalent to what was previously exposed via the metadata API.

If you have the required access, please convert the attached query into a SQL view to simplify and standardize consumption.




  

Thanks,  
Julio Diaz





Hi Pradeep, Venkat,

I have tested the Power BI deployment pipeline, and it is working as expected. The model is being automatically deployed to AppDev under the corresponding node (MTRC-MR-DataModels-Dev).

A few notes from the testing:

1. In the TMDL files, I updated the Databricks database parameter name from “Database” to “Databricks_Database” to align with the naming convention used for the other parameters.
2. The Marvel SPN (Client ID: 5664ac2a-27d8-4c6d-b1d1-79addcc61899) currently does not have access to configure the gateway. Specifically, it lacks permissions for the connection named “Con_CubiQ_Dev”. Please refer to the error shown in the screenshot below.
3. In AppDev, the model will be deployed automatically once the code changes are merged into the development branch. For higher environments, deployments will be manually triggered via the UBSDeploy portal.

I will re-test tomorrow once the required gateway access has been granted.

Thanks,  
Julio

