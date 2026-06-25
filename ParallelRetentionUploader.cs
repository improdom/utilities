Hi Team,

While converting the Calc29 MRV measure, TV Change on FX Slide - +/-20%, from MDX to DAX, I identified a dependency on an SSAS Multidimensional member property:

[Scenario].[Scenario Name].CURRENTMEMBER.PROPERTIES("Normal Shock")

This property is used in the calculation to retrieve a value associated with each Scenario Name and multiply it by the FX Product Delta measure. However, this “Normal Shock” property is not currently available in the CubIQ Power BI semantic model.

To support an equivalent DAX implementation, I recommend adding this property as a numeric column in the Scenario dimension, preferably as a hidden field. This would allow the DAX measure to reference the value directly while keeping the model clean for end users.

Without this column or an equivalent lookup table, the Calc29 logic cannot be fully reproduced in Power BI.

Thanks,
Julio
