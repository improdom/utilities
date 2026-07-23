Yes, we can retrieve and save the current report definition before each deployment. This would provide a recoverable snapshot of any changes made directly in the workspace.

We can then compare the current workspace definition with the last deployed version and the new version being released. Since PBIR stores pages, visuals, bookmarks, and other report components in separate structured files, changes made to different components could generally be merged automatically.

Where both the workspace user and the new deployment have modified the same component, the service should flag the conflict for review rather than overwrite either change automatically.

The proposed deployment flow would therefore be:

Back up the current report to ADLS → detect changes since the previous deployment → merge non-conflicting changes → flag conflicts for review → validate and deploy.
