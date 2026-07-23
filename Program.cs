# Self-Service Power BI Report Deployment Design

## 1. Overview

The solution separates report generation from report deployment.

The **Self-Service Reporting Application** allows users to define reports, including filters, layouts, visuals, and other report-specific settings. Based on the user’s selections, the application generates a Power BI Project (**PBIP**) package.

A separate API, called the **Power BI Deployment Service**, is responsible for versioning, publishing, configuring, and tracking the generated Power BI artifacts.

## 2. Main Components

### Self-Service Reporting Application

The Self-Service Reporting Application is responsible for:

* Capturing the user’s report definition.
* Capturing report filters and other configuration options.
* Generating the corresponding PBIP package.
* Sending the PBIP package and deployment metadata to the Power BI Deployment Service.
* Displaying the deployment result to the user.

### Power BI Deployment Service

Suggested name:

**Power BI Deployment Service**

Alternative names include:

* Power BI Artifact Publisher
* Report and Model Deployment API
* BI Deployment Orchestrator

The Power BI Deployment Service is responsible for:

* Receiving the PBIP package and deployment request.
* Validating the report and model definitions.
* Committing the PBIP files to the appropriate Git repository.
* Capturing the Git commit identifier.
* Publishing the report and semantic model to the corresponding Power BI workspace.
* Applying required Power BI configuration.
* Logging the deployment state and results in SQL Managed Instance.
* Supporting retries and deployment status tracking.

## 3. High-Level Interaction

```text
┌───────────────┐
│     User      │
└───────┬───────┘
        │ Defines report, filters and configuration
        ▼
┌──────────────────────────────────┐
│ Self-Service Reporting Application│
│                                  │
│ • Captures report definition     │
│ • Generates PBIP package         │
└────────────────┬─────────────────┘
                 │
                 │ PBIP package and deployment metadata
                 ▼
┌──────────────────────────────────┐
│ Power BI Deployment Service      │
│                                  │
│ • Validates package              │
│ • Commits PBIP to Git            │
│ • Publishes report/model         │
│ • Configures Power BI artifacts  │
│ • Tracks deployment status       │
└───────┬───────────────┬──────────┘
        │               │
        ▼               ▼
┌──────────────┐   ┌─────────────────────┐
│ Git Repository│   │ Power BI Workspace  │
│              │   │                     │
│ PBIP version │   │ Report and semantic │
│ and history  │   │ model deployment    │
└──────────────┘   └─────────────────────┘
        │
        │ Deployment and Git state
        ▼
┌─────────────────────┐
│ SQL Managed Instance│
│                     │
│ • Deployment status │
│ • Git commit ID     │
│ • Workspace IDs     │
│ • Errors and audit  │
└─────────────────────┘
```

## 4. Deployment Flow

1. The user creates or updates a report through the Self-Service Reporting Application.
2. The application generates a PBIP package containing the report definition, filters, visuals, and associated model configuration.
3. The application submits the PBIP package and deployment metadata to the Power BI Deployment Service.
4. The deployment service validates the request and records an initial deployment entry in SQL Managed Instance.
5. The PBIP package is committed to Git for version control and traceability.
6. The resulting Git commit identifier is stored in SQL Managed Instance.
7. The same PBIP version is published to the appropriate Power BI workspace.
8. The deployment service applies any required workspace, report, semantic model, connection, or refresh configuration.
9. The final deployment status and Power BI artifact identifiers are stored in SQL Managed Instance.
10. The deployment result is returned to the Self-Service Reporting Application.

## 5. Deployment States

The deployment service should track the request through states such as:

```text
Received
   ↓
Validated
   ↓
Git Committed
   ↓
Publishing
   ↓
Configuring
   ↓
Completed
```

Possible failure states include:

```text
Validation Failed
Git Commit Failed
Power BI Publish Failed
Power BI Configuration Failed
```

A report should not be published to Power BI unless the Git commit succeeds.

If the Git commit succeeds but the Power BI deployment fails, the deployment record should retain the Git commit identifier and allow the Power BI deployment to be retried using the same report version.

## 6. Deployment Metadata

Each deployment request should include metadata such as:

* Deployment ID
* Report name
* Semantic model name
* Target environment
* Target workspace
* PBIP package location or content
* Report version
* User or process requesting the deployment
* Generation timestamp
* Source application
* Optional deployment comments

The deployment record should additionally capture:

* Git repository
* Git branch
* Git commit identifier
* Power BI workspace ID
* Power BI report ID
* Power BI semantic model ID
* Deployment status
* Error details
* Start and completion timestamps

## 7. Design Rationale

This separation keeps the responsibilities clear:

* The **Self-Service Reporting Application** owns report creation.
* The **Power BI Deployment Service** owns versioning, deployment, configuration, audit, and operational state management.

Keeping the Git commit and Power BI publication within the same deployment service ensures that both operations remain coordinated. It also provides a direct relationship between the report stored in Git and the version deployed to Power BI.

## 8. Summary

The proposed architecture introduces a centralized Power BI Deployment Service that receives PBIP packages from the Self-Service Reporting Application.

The service commits each package to Git before publishing it to Power BI and records all deployment states in SQL Managed Instance. This provides clear ownership, consistent deployment behavior, retry support, version traceability, and a complete audit history.
