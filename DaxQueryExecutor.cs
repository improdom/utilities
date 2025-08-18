EVALUATE
VAR MaxBizDateTime =
    CALCULATE (
        MAX ( pbi_fact_risk_results_aggregated_vw[Business Date] ),
        ALL ( 'CoB Date' ),                                     -- ignore any date filters
        ALL ( pbi_fact_risk_results_aggregated_vw[Business Date] )
    )
VAR MaxCoBDate = DATEVALUE ( MaxBizDateTime )                   -- align to Date type
RETURN
ROW (
    "CoB Date", MaxCoBDate,
    "Report Value",
        CALCULATE (
            [Report Value],
            KEEPFILTERS ( TREATAS ( { MaxCoBDate }, 'CoB Date'[CoB Date] ) ),
            REMOVEFILTERS ( pbi_fact_risk_results_aggregated_vw[Business Date] )
        )
)
