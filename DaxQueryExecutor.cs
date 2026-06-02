
Socrates MDS CCY :=
VAR MaxAbsForSocratesAcrossCurrency =
    MAXX (
        ALL ( 'Market Data Set'[MDS Currency] ),
        CALCULATE (
            [Abs],
            'Slice'[Source System] = "Socrates",
            REMOVEFILTERS ( 'Risk Measure' )
        )
    )

VAR CurrentCurrencyAbs =
    CALCULATE (
        [Abs],
        'Slice'[Source System] = "Socrates",
        REMOVEFILTERS ( 'Risk Measure' )
    )

RETURN
IF (
    ISINSCOPE ( 'Market Data Set'[MDS Currency] ),
    IF (
        CurrentCurrencyAbs = MaxAbsForSocratesAcrossCurrency,
        CurrentCurrencyAbs,
        BLANK ()
    ),
    MaxAbsForSocratesAcrossCurrency
)




┌────────────────────────────────────────────┐
│            Reporting Framework             │
└────────────────────────────────────────────┘

            ┌──────────────────────┐
            │ Definition Parser    │
            └──────────┬───────────┘
                       │
                       ▼

            ┌──────────────────────┐
            │ Execution Context    │
            │ Parameters           │
            │ Datasets             │
            └──────────┬───────────┘
                       │
                       ▼

     ┌─────────────────────────────────────┐
     │          Calculation Engine         │
     │                                     │
     │ • Expressions                       │
     │ • Totals                            │
     │ • Matrix Aggregations               │
     │ • Conditional Formatting            │
     └─────────────────┬───────────────────┘
                       │
                       ▼

     ┌─────────────────────────────────────┐
     │         Component Factory           │
     └─────────────────┬───────────────────┘
                       │
      ┌────────────────┼─────────────────┐
      ▼                ▼                 ▼

┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│Table Builder│ │MatrixBuilder│ │ChartBuilder │
└──────┬──────┘ └──────┬──────┘ └──────┬──────┘
       │               │               │
       └───────────────┼───────────────┘
                       ▼

             ┌──────────────────┐
             │ QuestPDF Builder │
             └──────────────────┘





Subject: Expansion of MRV Reconciliation Coverage – Next Iteration

Hi Team,

As part of the CubIQ release plan, we are continuing to expand the reconciliation of MRV (Power BI measures) against the on-prem cube that CubIQ will replace.

So far, reconciliation has been limited to a subset of MRVs (MVP). The overall target remains to reconcile all 2,176 MRVs.

Following a detailed analysis, we are now ready to significantly extend the reconciliation scope in the next iteration:

An additional 1,708 MRVs have been identified and can be immediately included in reconciliation.
These include:
Non-MVP filter MRVs previously excluded
Most calculation-type MRVs (Calc types)
The remaining gap is limited to 45 MRVs, primarily complex/custom calculation types, which will be addressed in the May release.

This expansion represents a substantial increase in coverage and should allow us to begin a broader and more representative validation of CubIQ against the legacy cube.

I am attaching an Excel document that includes a pivot table with the detailed breakdown of MRVs by category and reconciliation eligibility.

Next Steps
QA team can begin preparing and executing reconciliation for the expanded MRV set.
Please review the attached breakdown and flag any concerns or dependencies.
We will continue to track and close the remaining 45 MRVs as part of the next release cycle.

Please let me know if you have any questions or need additional details.

Best regards,
Julio Diaz
