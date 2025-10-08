Unique identifiers for model relationships
Power BI’s star schema guidance states that when your dimension table doesn’t have a natural unique key, you should add a surrogate key to serve as the “one” side of relationships. 
Microsoft Learn

Dimensional modeling foundation
Ralph Kimball’s canonical work argues that “every join between dimension tables and fact tables … should be based on surrogate keys, not natural keys.” 
Kimball Group
+1

Better performance in Power BI
Using numeric surrogate keys (versus wide, composite business keys) improves join performance and reduces memory usage in BI contexts. 
Medium
+1

Practical examples in Power BI / community practice
The Power BI community forum discusses creating a surrogate key in a dimension and merging it into the fact, exactly the pattern I’m proposing. 
Power BI Community

In hybrid scenarios (Import + DirectQuery), folks use surrogate keys via Power Query merges to maintain consistency. 
Microsoft Fabric Community

Modern tooling and modeling patterns
Tools like dbt (when used for dimensional modeling) recommend surrogate keys so that models have a clean, manageable join key.
