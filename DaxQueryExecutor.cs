EVALUATE
FILTER(
    UNION(
        TOPN(1, 'pbi_fact_risk_results_aggregated_vw'),
        TOPN(1, 'Dim_Scenario'),
        TOPN(1, 'Dim_Country'),
        TOPN(1, 'Dim_Market_Segment')
        -- add more TOPN(1, 'Table') as needed
    ),
    FALSE()
)
