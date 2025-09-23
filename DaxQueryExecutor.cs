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








Subject: Potential fix for DQ attribute detection — tests progressing; request for validation in DAX Studio + appdev

Hi team,

Quick update on the DirectQuery (DQ) extra-query issue: initial tests look promising.

### What changed

I split the logic into two measures:

* **`IS_DIRECT_QUERY_ATTRIBUTE`** – detects when any DQ attribute is on the visual path (using `ISINSCOPE`/`ISFILTERED` checks).
* **`BASE_VALUE_USD`** – the main calc now *references* `IS_DIRECT_QUERY_ATTRIBUTE` to decide whether to run the CoB-date routing logic or fall back to a simpler aggregation when DQ attributes are present.

### Why separate the detection into its own measure

* **Performance & caching:** The detection boolean is computed once per cell and reused, reducing repeated predicate work inside the main measure.
* **Maintainability:** We can add/remove DQ columns in a single place without touching business logic.
* **Testability:** It’s easy to validate the detector in isolation (DAX Studio / Perf Analyzer) and compare paths.
* **Auditability:** Clear boundary between “when to route” vs “how to compute,” which helps code reviews and future refactors.

### Attachments

1. `BASE_VALUE_USD.dax`
2. `IS_DIRECT_QUERY_ATTRIBUTE.dax`
3. `BASE_VALUE_USD_DAX_STUDIO.dax` (standalone script for local testing)

### Venkat — request for validation

Please start with **DAX Studio** using `BASE_VALUE_USD_DAX_STUDIO.dax`:

1. **Scenarios to run**

   * A) Visuals using **only Import** attributes.
   * B) Visuals where **DQ attributes are on the axis**.
   * C) Visuals with **DQ attributes only as slicers/filters** (no axis use).
   * D) Mixed visuals (Import + DQ on rows/columns plus CoB filters).

2. **What to capture / compare**

   * Server Timings:

     * Formula Engine vs Storage Engine time
     * Query count (ensure we don’t trigger the redundant distinct-list query)
   * SQL folding in Query Plan:

     * Presence of **CoB filters** in generated SQL
     * Aggregation hits vs detail fallbacks
   * Databricks history:

     * Query count and runtimes per scenario
     * Repeated/duplicate queries (expect reduction)
   * Visual correctness:

     * Numbers match baseline for each scenario

3. **Acceptance criteria (initial)**

   * No functional regressions.
   * In scenarios with DQ attributes, total query count is stable or reduced.
   * CoB filter reliably appears in SQL where expected.
   * FE time does not materially increase vs current version.

After DAX Studio, please **deploy to `appdev`** and repeat a small battery of report-level tests (Perf Analyzer + Databricks history). Note any differences vs local runs (dataset/partition state, security filters, etc.).

### Notes & next steps

* The change is currently labeled as a **potential fix** while we finish coverage. I’ll confirm once we’ve exercised all test cases.
* If any scenario shows regressions (perf or correctness), we can adjust `IS_DIRECT_QUERY_ATTRIBUTE` or expand the sentinel set as needed.
* I’ll document the final decision and, if positive, prepare a controlled rollout plan.

Thanks, and Venkat—please share your results and traces when ready.


