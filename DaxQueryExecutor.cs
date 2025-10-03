Hi Amar,

As discussed, even though it may not be feasible to load all attributes of this dimension into memory, there are still clear benefits to splitting them into separate tables. Doing so will reduce the risk of unnecessary distinct queries on the detail table, improve Excel performance, and give us more control over query execution.

We propose the following approach:

Risk Factor: Holds the current in-memory attributes for this dimension – fewer, less granular attributes.

Risk Factor (Extended): Contains the remaining attributes, which will continue to run in DirectQuery mode.

Risk Factor 2: Holds the current in-memory attributes for this dimension – a second set of fewer, less granular attributes.

Risk Factor 2 (Extended): Contains the remaining attributes, which will also run in DirectQuery mode.

This separation ensures we manage memory usage effectively while avoiding the overhead of repeated distinct queries in the detail model. It also enhances user experience in Excel.
