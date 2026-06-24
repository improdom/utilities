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
        VAR NormalShock =
            'Scenario'[Normal Shock]

        VAR FXDelta =
            CALCULATE (
                [FX Product Delta Primary and Translational]
            )

        VAR AdjustedFXRate =
            CALCULATE (
                [TV Change Product Delta Adjusted FX Rates]
            )

        VAR Temp1 =
            FXDelta * NormalShock

        VAR Temp2 =
            IF (
                ISBLANK ( AdjustedFXRate ),
                BLANK (),
                Temp1 + AdjustedFXRate
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
