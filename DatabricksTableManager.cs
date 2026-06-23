VAR Temp1 =
    MINX (
        CALCULATETABLE (
            VALUES ( 'Scenario'[Stress Magnitude] ),
            REMOVEFILTERS ( 'Scenario'[Scenario Name] ),
            REMOVEFILTERS ( 'Scenario'[Stress Magnitude] ),
            'Scenario'[Stress Magnitude Value] >= -0.15,
            'Scenario'[Stress Magnitude Value] <= 0.15
        ),
        CALCULATE ( [Structured Product Delta Family] )
    )
RETURN
    IF (
        ISBLANK ( Temp1 ),
        BLANK (),
        MIN ( 0, Temp1 )
    )
