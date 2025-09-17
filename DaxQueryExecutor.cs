private static readonly Dictionary<string, Type> TypeMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
{
    { "String", typeof(string) },
    { "Int32", typeof(int) },
    { "Int64", typeof(long) },
    { "Boolean", typeof(bool) },
    { "DateTime", typeof(DateTime) },
    { "Decimal", typeof(decimal) },
    { "Double", typeof(double) },
    { "Float", typeof(float) }
    // Add more as needed
};

...

Type dataType = TypeMap.ContainsKey(g.DataType) ? TypeMap[g.DataType] : typeof(object);
theParams.Columns.Add(new DataColumn("column", dataType));




Here’s the updated version including the environment and verification details:

---

**Subject:** New VertiPaq Dictionary Warm-Up Process

Hello \[Manager’s Name],

We have introduced a new **VertiPaq Dictionary warm-up** process that runs immediately after each dataset refresh, while the model remains in writer mode and isolated from users.

The process operates in two phases:

* **Phase 1 – Fact table:** Warm up the aggregation (fact) table first, since it is the largest and most query-intensive. We target column dictionaries and representative data segments rather than scanning the entire table, ensuring efficient memory utilization. (\~7 minutes)
* **Phase 2 – Dimensions:** Warm up all dimension tables, pre-loading their dictionaries and attributes used for filtering and joins. (\~1.5 minutes)

This results in the entire semantic model being primed in memory in \~8.5 minutes, so that when the model is made available to users, queries execute consistently without delay.

The approach was tested in the **Hub 1 node of the Preprod environment**, and I verified that it successfully resolves the performance issue we have been observing. We will continue to refine this process to adapt to changes in model size or usage patterns, but the initial results are effective and reliable.

Best regards,
\[Your Name]

---

Do you want me to also include a short **recommendation to roll this into Production** next, or leave it as an informational update?

