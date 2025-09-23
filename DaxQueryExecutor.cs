Here’s an updated draft of the email based on your actual measure implementation in the screenshot:

⸻

Subject: Potential fix to avoid extra DISTINCT query in DQ scenarios — testing in progress

Hi team,

We’ve implemented a potential fix to address the redundant SELECT DISTINCT query that appears in Databricks when Position[Currency List] is placed on the axis together with our existing measure logic.

What changed

In the measure we introduced a conditional gate:

VAR isDirectQueryInUse =
    ISINSCOPE ( 'Σ Position RF'[Currency List] ) ||
    ISFILTERED ( 'Σ Position RF'[Currency List] )

RETURN
IF (
    isDirectQueryInUse,
    SUM ( 'Σ Position RF'[fact_value_usd] ),   -- lean branch
    DataValue                                  -- full branch with in-memory logic
)

	•	If Currency List is in scope (on axis/slicer), the measure uses a simplified SUM path that folds to a single grouped SQL — eliminating the extra DISTINCT probe.
	•	Otherwise, it runs our original DataValue logic (including the in-memory vs. updated-ts branching).

Why this matters
	•	Keeps the output identical, but the query plan is cleaner when Currency List is used.
	•	Early tests confirm that the duplicate DISTINCT query disappears and only one folded SQL is generated.

Next steps
	•	Venkat, can you run validation on the key scenarios:
	1.	Visuals with Currency List on the axis.
	2.	Visuals without Currency List.
	3.	Mixed filters (CoB slicers, desk/book, etc.).
	•	Please check Databricks query history to confirm that only one SQL statement is fired per visual.

Once all test cases have been covered, we’ll confirm whether this fix is stable enough to promote to the shared measure set.

Thanks,
Julio

⸻

Do you want me to also include a before/after query plan snapshot description (so the team sees what we expect in Query History), or keep it concise as above?
