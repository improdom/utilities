Hi [Manager Name],

Our recent GitLab/Nexus vulnerability scan flagged the Toastr JavaScript library (currently v2.1.1) and recommends upgrading to version 2.1.3.

After verification, version 2.1.3 is not officially available in NuGet, npm, or the original GitHub repository. The latest published release from the maintainers remains v2.1.1. This suggests the scanner is referencing a version that is not distributed through the standard channels we use.

Given this, we have three practical options:

**Option 1 – Replace the library with a maintained alternative**
Example: SweetAlert2 or Bootstrap Toast.

Advantages:

* Removes the current vulnerability finding.
* Reduces future governance friction.
* Moves us to an actively maintained dependency.

Disadvantages:

* Requires development and regression testing effort.
* Potential minor UI adjustments.
* Small implementation timeline impact.

**Option 2 – Submit a formal security exception / risk acceptance**

Document that no newer official version exists, validate whether the reported vulnerability is exploitable in our implementation, and request a governance deferral.

Advantages:

* Minimal development effort.
* Fast resolution if approved.
* No UI or functional changes required.

Disadvantages:

* Requires formal review and approval process.
* May need periodic re-justification in future scans.
* Does not modernize the dependency.

**Option 3 – Rewrite or implement a lightweight in-house notification component**

Develop a small custom notification utility that covers our current usage (basic success/error/info messages) and remove the third-party dependency.

Advantages:

* Full control over implementation.
* Eliminates dependency-related vulnerability findings.
* Simplifies future security governance for this component.

Disadvantages:

* Requires development and testing effort.
* We assume long-term maintenance responsibility.
* Must ensure secure coding standards and UI consistency.

Please let me know which direction you would prefer so we can proceed accordingly.

Best regards,
Julio Diaz
