Hi Team,

We will begin Phase 1 of performance validation for the CubIQ Power BI model immediately, focusing on stress testing to identify the system’s breaking point.

**Phase 1 – Stress Testing (Breaking Point Identification)**
We will use the existing set of 500 DAX queries currently available in the Stress Test Tool. The objective of this phase is to determine the maximum number of queries the system can handle at the same time before performance degrades.

In the tool, concurrency is calculated as:

Threads × Concurrency setting
Example:
2 threads × 3 concurrency = 6 queries running simultaneously.

We will gradually increase concurrency levels and monitor:

* Response times
* Timeout and failure rates
* The highest stable concurrency level before significant degradation

Initial testing showed that:

* 10 threads × 10 concurrency (100 concurrent queries) resulted in timeout errors.

The goal is to systematically increase concurrency and clearly identify the point at which the system starts to fail or slow down significantly.

**Important – Test Isolation Requirement**
These stress tests must be executed in isolation. During test execution, please ensure no other workloads or ad-hoc queries are running against the Databricks environment. This is required to ensure results accurately reflect CubIQ capacity limits and are not influenced by external activity.

**Phase 2 – Load Testing (Next Week)**
Next week, we will perform load testing by simulating concurrent users running queries continuously for a sustained period of time. The goal will be to measure how the system performs under steady, real-world usage, including overall throughput, response times, and system stability during ongoing activity.

Regards,
Julio
