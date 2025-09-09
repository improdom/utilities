Hi Pushkar,

For the PoC, we will need Fabric capacity equivalent to a P4 node. This will provide a comparable baseline to our current setup and allow us to validate performance when moving our semantic model (aggregation + detail tables in DirectQuery) into hybrid and Direct Lake modes.

We recommend using the Subscription model rather than PAYG, since the PoC will run for approximately 3 months and will involve loading several terabytes of data and running sustained queries. Subscription provides predictable costs and consistent dedicated resources throughout the PoC.

Additionally, we will need E5 license access for 5 users to properly validate end-to-end feasibility.

Thanks,
Julio
