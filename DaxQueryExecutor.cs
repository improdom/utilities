DEFINE
    VAR latestCob = CALCULATE(MAX('pbi_fact_risk_results_aggregated_vw'[Business Date]))

EVALUATE
SUMMARIZECOLUMNS(
    // Business_Hierarchy hierarchy
    Desk[Business Group],
    Desk[Business Unit],
    Desk[Business Area],
    Desk[Sector],
    Desk[Segment],
    Desk[Function],
    Desk[Desk],
    Desk[Sub-Desk],
    Desk[Cost Center],
    Desk[Book],
    KEEPFILTERS(
        TREATAS(
            { latestCob },
            'CoB Date'[CoB Date]
        )
    )
)
