Hi [Name],

I noticed that Power BI is automatically adding the fn_left(measure, 4000) function when joining tables using string-based keys. This happens because Power BI detects unbounded string types (e.g., STRING or VARCHAR(MAX)) and truncates them to ensure compatibility during join pushdown.

To avoid this behavior and improve performance, we should explicitly cast join columns to a fixed-length string type in Databricks—for example:

CAST(measure AS STRING(200))


Defining the column length removes the need for Power BI to wrap it in fn_left().

Additionally, as a best practice, joins should use surrogate keys of type INT instead of string keys whenever possible. Integer-based joins are faster, more efficient, and lead to better query folding and performance in both Databricks and Power BI.

Regards,
Julio
