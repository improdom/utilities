Subject: Proposed Approach for Report Publishing Enhancements

Hi all,

Following our architecture discussions, we are taking an approach that further improves the overall design by clearly separating report generation from deployment responsibilities. By extending the existing deployment service instead of introducing deployment logic into the Self-Service application, we can reduce complexity, improve maintainability, and leverage components that are already proven and in place.

To meet the new report publishing requirements within our timeline, I'd like to propose splitting the implementation into two related but independent areas while leveraging the deployment service we already have in place.

Self-Service – Responsible for generating the PBIP package based on the user's report definition (filters, visuals, configuration, etc.).
Report Deployment Service (existing) – Extend the current semantic model deployment service to also version the PBIP in Git, publish and configure the report/model in the target Power BI workspace, and log deployment status in SQL Managed Instance.

This approach minimizes the changes required in Self-Service by keeping report generation separate from deployment concerns, while reusing and extending the existing deployment service instead of building new deployment functionality from scratch. It also centralizes deployment logic (Git, Power BI publishing, configuration, and auditing) in a single reusable component.

Another advantage is that Lokesh and I can work independently and in parallel, with each of us focusing on one area while keeping the integration between the two components well defined.

I've attached a high-level diagram illustrating the proposed flow. We can review it together and discuss any feedback before moving forward.

Thanks,
Julio
