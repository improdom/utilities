/* START QUERY BUILDER */
EVALUATE
VAR MaxBizDate =
    CALCULATE (
        MAX ( pbi_fact_risk_results_aggregated_vw[Business Date] ),
        ALL ( pbi_fact_risk_results_aggregated_vw[Business Date] )   -- get max available date
    )
RETURN
SUMMARIZECOLUMNS (
    'CoB Date'[CoB Date],
    KEEPFILTERS (
        FILTER (
            ALL ( pbi_fact_risk_results_aggregated_vw[Business Date] ),
            pbi_fact_risk_results_aggregated_vw[Business Date] = MaxBizDate
        )
    ),
    "Report Value", [Report Value]
)
ORDER BY
    'CoB Date'[CoB Date] ASC
/* END QUERY BUILDER */
