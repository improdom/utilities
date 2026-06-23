IR Zero Delta Bond Future Basis =
VAR Temp1 =
    IF(
        [IR Zero Delta Base - Bond Futures] < 0,
        -1,
        1
    )

VAR Temp2 =
    IF(
        [IR Zero Delta Base - Govt./Treasury Bond] < 0,
        -1,
        1
    )

VAR Temp3 =
    ABS(
        COALESCE([IR Zero Delta Base - Bond Futures], 0)
    )

VAR Temp4 =
    ABS(
        COALESCE([IR Zero Delta Base - Govt./Treasury Bond], 0)
    )

RETURN
    IF(
        ISBLANK([IR Zero Delta Base - Bond Futures])
            && ISBLANK([IR Zero Delta Base - Govt./Treasury Bond]),
        BLANK(),
        IF(
            Temp1 = Temp2,
            0,
            MIN(Temp3, Temp4)
        )
    )
