Hi Michal,

Thanks for the clarification and for pointing me to the communications and instructions. I wasn’t aware this was the root cause, but we’ll update our repositories accordingly.

Thanks for your help.

Regards,
Julio






I need to write an c#?application which will run as a job at night at a scheduled time.
This application responsibility to synchronize reports in pbir format currently deployed in power bi and service and similar files stored in ADLS.
Basically, users will generate a report from an application named self-service, they will select the semantic model they want to run their reports on, apply filters, select attributes, etc. when they click on save a report definition in pbir is generated, published to power bi in a PPL environment, and files also uploaded to ADLS for tracking and traceability.
There is a workflow in the application from where reports can be approved and then moved to Prod environment.
In PPL users can make changes in report online, which will bring the report in the power bi service out of sync with the last version in ADLS.

The job will read all active and available reports from SQL Mi table, it will then download the pbir files from ADLS and also from power bi ppl workspace, it will compare each one of   the set of files for differences, if you identify a difference then it will update ADLS with the changes currently in the power bi service so both sources are un sync.
I need your help preparing the requirements, please ask any relevant questionPBIR Report Publishing with ADLS Version Management





I've completed a prototype and validated the core functionality for managing PBIR file versioning using native Azure Blob Versioning. The prototype supports uploading, updating, downloading, restoring previous versions, and handling complete PBIR project structures while preserving folder hierarchy
