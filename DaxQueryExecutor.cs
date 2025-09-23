Hi all,

I wanted to share a quick update on the changes around DirectQuery attributes.

We now have a separate measure, IS_DIRECT_QUERY_ATTRIBUTE, that detects when DQ attributes are in use. The main measure, BASE_VALUE_USD, references this detection measure to decide when to apply the CoB date routing logic.

Separating the detection has a couple of advantages:

it avoids repeating the same checks inside the business logic,

makes it easier to maintain if the set of DQ columns changes,

and allows us to test the detection logic independently.

I’ve attached three files for review:

BASE_VALUE_USD.dax

IS_DIRECT_QUERY_ATTRIBUTE.dax

BASE_VALUE_USD_DAX_STUDIO.dax

Venkat – could you start by testing with DAX Studio using the provided script, then move on to a deployment in appdev? Please check both correctness of results and performance (query counts, folding, CoB filters in SQL, etc.).

So far, local tests look good. We’ll confirm once all scenarios have been exercised.

Thanks,
[Your Name]
