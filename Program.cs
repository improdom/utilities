Hi Durga / Pradeep,

This issue has now been resolved.

As discussed, I reviewed both the logic used to evaluate the trigger schedule and the mechanism responsible for loading the data. The root cause was related to a metadata issue in the on-prem Oracle environment.

While the issue originated upstream, we will introduce additional safeguards on our side to ensure our processes remain protected from similar external conditions. We will also implement an optimization so that data movement is triggered only when updates are detected.

These improvements will be included in the March 27th release, and I will be closely monitoring this change after deployment to ensure it behaves as expected.

Thanks,
Julio Diaz
