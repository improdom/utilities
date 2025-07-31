Subject: Task: Implement Power BI Load Balancer Data Model in Refresh Service

Hi Apurva,

Please find attached the data model design document for the Power BI Load Balancer functionality (PowerBI_LoadBalancer_ModelDesign.docx).

Kindly work on the following tasks based on this design:

✅ Tasks
1. Create the following tables in PostgreSQL:

worker_model

model_state

model_role_history

model_refresh_log

Refer to the column types and constraints in the attached document.

2. Update the Power BI Refresh Service to support the new model:
a) Create Entity classes for each of the above tables
b) Update the DbContext class to configure these entities
c) Create a Repository class that implements the necessary CRUD operations for each entity

Let me know once the implementation is complete or if you run into any questions.

Thanks,
Julio
