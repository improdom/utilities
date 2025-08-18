EVALUATE
VAR MaxBizDate =
    CALCULATE ( MAX ( pbi_fact_risk_results_aggregated_vw[Business Date] ),
                ALL ( pbi_fact_risk_results_aggregated_vw[Business Date] ) )
RETURN
ROW (
    "CoB Date", MaxBizDate,
    "Report Value",
        CALCULATE ( [Report Value],
                    TREATAS ( { MaxBizDate }, 'CoB Date'[CoB Date] ),
                    REMOVEFILTERS ( pbi_fact_risk_results_aggregated_vw[Business Date] ) )
)
