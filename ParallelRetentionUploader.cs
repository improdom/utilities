ComplexCalc16 =
VAR Dep1 = [Dependent Measure 1]
VAR Dep2 = [Dependent Measure 2]
VAR TVStress = [TV Change Credit Index Basis Stress Tight]
RETURN
    IF(
        ISBLANK(Dep1) && ISBLANK(Dep2),
        BLANK(),
        IF(
            ISBLANK(Dep1),
            MIN(0, Dep2),
            IF(
                ISBLANK(TVStress),
                MIN(0, Dep2),
                MINX(
                    {
                        (0),
                        (Dep1),
                        (Dep2)
                    },
                    [Value]
                )
            )
        )
    )
