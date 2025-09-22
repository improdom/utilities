// ---- Helper: selected CoB set from slicers/filters ----
Selected CoB Ids :=
    ALLSELECTED ( 'CoB Date'[CoB Date ID] )

Selected CoB Count :=
    COUNTROWS ( [Selected CoB Ids] )

// ---- Helper: how many of those CoBs actually exist in the FACT ----
// Ignore ALL filters (including unknown dimensions) then re-apply ONLY the CoB set via TREATAS.
// This avoids having to name every dimension and keeps the check to a tiny scalar query.
Available CoB Count :=
CALCULATE (
    DISTINCTCOUNT ( 'Σ Position RF'[CoB Date ID] ),
    ALL ( 'Σ Position RF' ),                                  -- wipe all filters hitting the fact
    TREATAS ( [Selected CoB Ids], 'Σ Position RF'[CoB Date ID] )  -- re-apply just the CoB set
)

All Selected CoBs Available :=
    [Selected CoB Count] = [Available CoB Count]

// ---- Final measure: pure aggregate; no REMOVEFILTERS on the fact ----
[Value USD] :=
VAR Base := SUM ( 'Σ Position RF'[fact_value_usd] )
RETURN
    IF (
        [All Selected CoBs Available],
        Base,
        CALCULATE ( Base, 'Σ Position RF'[updated ts] <> BLANK () )
    )
