@startuml
!theme plain
actor User
entity "Query Space API" as Scheduler
entity "Query Executor" as Executor
entity "Power BI" as PBI


User -> Scheduler : Registers query 

User -> Scheduler : Sets data readiness configuration
Scheduler -> Executor : Executes query based on schedule
User -> Executor : Monitors execution status

Executor -> User : Notifies query completion and status

User -> PBI : Retrieves data from Power BI
@enduml


Automation & Efficiency: Schedule and execute queries automatically, reducing manual effort and minimizing errors.

Scalable Data Management: Store and manage large datasets accumulated over time, ensuring seamless access and long-term retention.

Improved Monitoring & Access: Track query execution status and easily retrieve data for reporting and analysis, ensuring timely and accurate insights.
