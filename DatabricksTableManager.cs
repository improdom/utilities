Hi Anup,

As discussed, we’ll move forward with setting up the nightly background job scheduling directly in Kubernetes.

Since the plan is to migrate other similar scheduled jobs from ADF to Kubernetes, it makes sense to implement this job in Kubernetes from the beginning rather than introducing an ADF dependency that we would need to migrate later. This will keep the implementation aligned with the target architecture and avoid additional work in the future.

QA is currently triggering the job manually through Swagger. The Kubernetes setup is in progress, with the plan to have the automated scheduling completed tomorrow.

Thanks, Anup, for helping us get the appropriate infrastructure in place.

Regards,
Julio
