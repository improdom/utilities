Hi Team,

While reviewing the recent issue with missing Power BI changes in GitLab, I identified the likely root cause.

The repository currently contains a PBIX file that appears to be used for development. The changes are then manually converted to TMDL format and committed to the repository for deployment. In this setup, the PBIX effectively becomes the source of truth, while the TMDL files are treated as deployment artifacts.

This approach creates two problems:

1. PBIX is a binary file and cannot be properly version-controlled or diffed in GitLab.
2. Maintaining both PBIX and TMDL in parallel introduces a risk of them getting out of sync if both are not consistently updated.

To avoid these issues, we should use PBIP as the primary development format instead of PBIX. PBIP works natively with TMDL and allows the model files to be stored in a text-based structure that can be properly tracked, reviewed, and managed in GitLab. This would eliminate the need for manual conversions and reduce the risk of inconsistencies.

I will schedule a meeting to walk through how PBIP and TMDL should be structured in the GitLab repository and clarify the recommended development workflow going forward.

Please let me know if you have any concerns in the meantime.

Best regards,
Julio Diaz
