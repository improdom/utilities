/* START QUERY BUILDER */
DEFINE
MEASURE 'Risk Value'[MyMeasure] =
VAR SelectedCoBs =
    VALUES ( 'CoB Date'[CoB Date ID] )              -- dates used in the visual

-- scalar flag from the small Import table (never scans the fact)
VAR isInMemory :=
    CALCULATE (
        -- If multiple CoBs are on the visual, we require ALL to be in memory.
        -- MIN works if IsInMemory is 1/0; use AND across the set.
        MIN ( 'CoB Status'[IsInMemory] ),
        KEEPFILTERS ( TREATAS ( SelectedCoBs, 'CoB Status'[CoB Date ID] ) )
    ) = 1

RETURN
-- Single aggregation; branch only changes a simple predicate
CALCULATE (
    SUM ( 'Σ Position RF'[fact_value_usd] ),
    -- push the CoB filter to the fact as an equality predicate (folds to WHERE)
    KEEPFILTERS ( TREATAS ( SelectedCoBs, 'Σ Position RF'[CoB Date ID] ) ),
    -- if not in memory, add the updated-ts predicate (also folds to WHERE)
    IF ( isInMemory, TRUE (), 'Σ Position RF'[updated ts] <> BLANK () )
)
/* END QUERY BUILDER */
