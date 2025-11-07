Dear Team,

Following the transition of our project to the new Reporting and Monitoring team, I’ve reviewed the existing components and identified the key services required for continued support and development.
The objective is to reuse as much of the existing implementation as possible, rebrand these components, and migrate them under the CubIQ architecture — using new GitLab repositories and dedicated Azure/Kubernetes infrastructure.

This approach will allow us to maintain stability while standardizing all services around a single, consistent metadata-driven framework for Power BI automation and Market Risk reporting.

Below is the list of core services identified for migration and rebranding under CubIQ:

Planned CubIQ Services

1. CubIQ Metadata Catalog
Serves as the central metadata store and semantic contract across all CubIQ components.
It contains all the metadata required to build and manage the Power BI semantic model — including source-to-target mappings, business-friendly names, table structures, folders, storage modes, and DAX base measures.
This catalog defines the presentation layer for Power BI users and acts as the contract between Databricks and Power BI development, enabling automation and clear separation of responsibilities.

2. CubIQ Semantic Processor
A Power BI–powered component that uses metadata from the Catalog to prepare and structure the semantic layer.
It applies business naming conventions, creates hierarchies and display folders, builds base measures, and ensures consistency across the semantic model using the Power BI Tabular Object Model (TOM) and XMLA endpoints.

3. CubIQ MRV Calculation Engine
Responsible for dynamically generating Market Risk Value (MRV) measures based on business-provided definitions.
It automates the creation of DAX expressions, ensuring consistent analytical logic and repeatability across all MRV metrics in the CubIQ semantic model.

4. CubIQ PBI Deployment Service
Handles the end-to-end deployment of Power BI semantic models to the Power BI Service.
This service builds, publishes, and configures datasets, sets up credentials and gateway connections, and manages environment-specific variables for Dev, UAT, and Production environments.

5. CubIQ Data Refresh Controller
Orchestrates and monitors dataset refresh operations to ensure that Power BI models remain synchronized with Databricks and other upstream data sources.
It integrates with event triggers and readiness signals, manages dependencies, and handles error recovery and retry logic for stable refresh cycles.

6. CubIQ Recon Service
Ensures data consistency and integrity across all layers of the Market Risk data pipeline.
It reconciles values — including MRVs — between the Databricks source tables, the Power BI semantic model, and the on-prem SSAS cube.
When discrepancies exceed tolerance thresholds, it flags the affected feed and triggers notifications for mitigation.
The Recon Service provides end-to-end assurance that the data presented in Power BI accurately reflects the authoritative sources at every stage.

Next Steps

Prepare dedicated GitLab repositories under the CubIQ namespace.

Define the migration scope, dependencies, and sequence for each component.

Configure Azure and Kubernetes infrastructure to host the services.

Standardize CI/CD and environment configurations in alignment with CubIQ architecture.

This rebranding and migration will consolidate our existing functionality into a unified, scalable, and metadata-driven platform — minimizing duplication and strengthening alignment across Market Risk systems.
I’ll follow up with the proposed repository structure and an initial migration plan for review.

Best regards,
Julio Diaz
Market Risk Reporting & Monitoring
Investment Bank – UBS
