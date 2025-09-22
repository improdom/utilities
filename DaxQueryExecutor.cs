MEASURE 'Risk Value'[MyMeasure] :=
VAR SelectedCoBs   = VALUES ( 'CoB Date'[CoB Date ID] )
VAR AllCoBsPresent =
    CALCULATE (
        -- compare distinct CoBs *within current filters* to selected set
        COUNTROWS ( SelectedCoBs )
            = DISTINCTCOUNT ( 'Σ Position RF'[CoB Date ID] ),
        -- NO ALL(...) here; keep it scoped
        KEEPFILTERS ( TREATAS ( SelectedCoBs, 'Σ Position RF'[CoB Date ID] ) )
    )
RETURN
CALCULATE (
    SUM ( 'Σ Position RF'[reporting_value] ),
    KEEPFILTERS ( TREATAS ( SelectedCoBs, 'Σ Position RF'[CoB Date ID] ) ),
    IF ( AllCoBsPresent, TRUE(), 'Σ Position RF'[updated ts] <> BLANK() )
)
