Power BI Performance / Load / Stress Testing — what each one means

Performance testing
Goal: How fast is the model for a given workload?
Strategy: run a fixed set of representative DAX queries (same filters, same result sizes), measure latency (p50/p95/p99), repeat across a few runs to reduce noise. Include a warm cache run and (optionally) a cold-ish run.

Load testing
Goal: How does the model behave under “expected” concurrent usage?
Strategy: simulate realistic concurrency patterns (e.g., 5, 10, 20 concurrent users) with realistic pacing (not just a burst), measure throughput (queries/sec), latency percentiles, and error rates over a sustained window (10–30 minutes). The point is “steady state.”

Stress testing
Goal: Where is the breaking point?
Strategy: ramp concurrency upward (1 → 5 → 10 → 20 → 40…) until you hit unacceptable latency (the “knee” in the curve) or failures (timeouts, memory pressure, capacity saturation). Record the max stable throughput and the failure mode.

REST API vs ADOMD.NET for these tests

REST API (Datasets ExecuteQueries)

Path: goes through the Power BI HTTP/API layer first, then the dataset runs the DAX.

What it’s great for: validating application integration behavior (HTTP latency, retries, throttling, payload limits).

Risk for load/stress: the REST API has built-in throttling/limits (e.g., per-user request limits and response size limits). Under high concurrency, you can hit API limits before the capacity is truly saturated—so you end up measuring “API gateway behavior” more than “model/capacity behavior.”

ADOMD.NET (XMLA / Analysis Services protocol)

Path: connects much more directly to the dataset engine (same family of connectivity used by tools like SSMS/DAX Studio).

What it’s great for: measuring semantic model engine + capacity behavior (Formula/Storage Engine contention, cache effects, CPU/memory pressure) with less interference from HTTP API throttles.

Ideal for: performance/load/stress testing where the question is “How much can the model/capacity handle?”

Recommended strategy + which one to pick

Pick ADOMD.NET as the primary harness for Power BI performance/load/stress testing of the model and capacity.
Use it to build your core curves:

Concurrency ramp tests (find the knee point)

Sustained load tests (stability over time)

Latency percentiles (p50/p95/p99) and throughput

Use REST API only as a secondary mode if you also need to prove how a downstream HTTP-based application would behave (throttling, retry policy, API constraints). That’s a different question than capacity performance.
