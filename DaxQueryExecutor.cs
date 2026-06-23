ARR v Govt Basis =
VAR GovtSpreadRaw =
    COALESCE([IR Zero Delta - Govt], 0)
        + COALESCE([IR Spread Funding Delta ARR], 0)

VAR HasGovtSpread =
    NOT (
        ISBLANK([IR Zero Delta - Govt])
            && ISBLANK([IR Spread Funding Delta ARR])
    )

VAR Temp1 =
    IF(GovtSpreadRaw < 0, -1, 1)

VAR Temp2 =
    IF([IR Zero Delta - ARR] < 0, -1, 1)

VAR Temp3 =
    ABS(GovtSpreadRaw)

VAR Temp4 =
    ABS(COALESCE([IR Zero Delta - ARR], 0))

RETURN
    IF(
        NOT HasGovtSpread
            && ISBLANK([IR Zero Delta - ARR]),
        BLANK(),
        IF(
            Temp1 = Temp2,
            0,
            Temp1 * MIN(Temp3, Temp4)
        )
    )
    
    
    
    ARR v Govt Basis =
VAR GovtSpreadRaw =
    [IR Zero Delta - Govt]
        + [IR Spread Funding Delta ARR]

VAR Temp1 =
    IF(
        GovtSpreadRaw < 0,
        -1,
        1
    )

VAR Temp2 =
    IF(
        [IR Zero Delta - ARR] < 0,
        -1,
        1
    )

VAR Temp3 =
    ABS(
        COALESCE([IR Zero Delta - Govt], 0)
            + COALESCE([IR Spread Funding Delta ARR], 0)
    )

VAR Temp4 =
    ABS(
        COALESCE([IR Zero Delta - ARR], 0)
    )

RETURN
    IF(
        ISBLANK(GovtSpreadRaw)
            && ISBLANK([IR Zero Delta - ARR]),
        BLANK(),
        IF(
            Temp1 = Temp2,
            0,
            MIN(Temp3, Temp4)
        )
    )
    
    
    
    
    
    
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
  
  
  
Hi Ramesh,

As discussed, we removed the list of MRVs approved by MRO/TRO from the CubIQ semantic models. During reconciliation testing, however, we identified several dependency warnings indicating that some of these MRVs are referenced by other MRV definitions.

This suggests that a subset of the MRVs slated for removal are being used as intermediate calculations or dependencies within other active MRVs. Removing them physically from the model could therefore introduce unintended side effects, including calculation failures and reconciliation discrepancies.

To mitigate this risk while we complete a full dependency assessment, we propose the following approach:

Retain the MRVs in the semantic models temporarily.
Hide the MRVs from end users so they are no longer available for reporting or analysis.
Preserve all existing MRV dependencies and calculation chains.
Conduct a complete dependency review to identify and remediate any downstream references before permanent removal.

This approach achieves the primary objective of preventing user access to the deprecated MRVs while ensuring model stability and avoiding potential impacts to reconciliation results.

Once the dependency analysis is completed and all references have been addressed, we can proceed with the permanent removal of the affected MRVs from the models.

@Danelian, Nellia, please redeploy the MRV Builder Helm chart using version 26.1.29, which contains the configuration changes required to implement this update.

Please let me know if there are any concerns or if further discussion is required before proceeding.

Thanks & Regards,

Julio Diaz
Director – Market Risk Platforms
UBS
