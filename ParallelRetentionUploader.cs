TV Change on FX Slide - +/-20% :=
VAR Temp3 =
    MINX (
        FILTER (
            ALL ( 'Scenario' ),
            'Scenario'[Scenario Group] = "FX"
                && 'Scenario'[Stress Magnitude] >= -0.20
                && 'Scenario'[Stress Magnitude] <= 0.20
                && 'Scenario'[Stress Magnitude] <> 0
        ),
        VAR Temp1 =
            CALCULATE (
                [FX Product Delta Primary and Translational],
                'Scenario'[Scenario Name] = "Normal Shock"
            )

        VAR Temp2 =
            IF (
                ISBLANK ( [TV Change Product Delta Adjusted FX Rates] ),
                BLANK (),
                Temp1 + [TV Change Product Delta Adjusted FX Rates]
            )

        RETURN
            Temp2
    )

RETURN
    IF (
        ISBLANK ( Temp3 ),
        BLANK (),
        MIN ( 0, Temp3 )
    )
