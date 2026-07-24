Hi Anupam,

I was thinking about another approach that could help preserve self-service changes while still allowing reports to be centrally managed.

Instead of generating the report entirely from metadata, we could use the current PBIR definition downloaded from the workspace as the starting point. Self-Service would expose only the relevant report structure (attributes and filters) to the user, and then patch those changes back into the original PBIR definition while preserving visuals, bookmarks, IDs, formatting, and all other report components.

The high-level flow would be:

Download the current PBIR definition from the workspace.
Keep the original PBIR JSON as the base.
Parse and extract only the attributes and filters that the Self-Service designer needs to display, together with any additional saved metadata.
Allow the user to modify those elements.
Patch the changes back into the original PBIR JSON.
Preserve all other report components unchanged.
Validate the updated report and redeploy it. As part of the validation, verify that all attributes required by existing visuals are still present. If any required attributes have been removed, notify the user of the potential breaking changes before deployment.

This approach is technically feasible, but it would require enhancing Self-Service to support PBIR round-tripping instead of generating the report entirely from metadata.

I think this would provide a cleaner way to preserve self-service changes while minimizing the risk of overwriting existing report customizations.

Thanks,
Julio
