Hi [Manager Name],

I would like to align expectations around the previously mentioned 90 QPS baseline so that we have accurate performance information when communicating with stakeholders.

QPS (Queries Per Second) represents how many queries the system can complete every second on a sustained basis. It is directly determined by two factors: how many queries can run at the same time (concurrency) and how long each query takes.

The relationship is:

QPS ≈ Concurrency ÷ Average Query Duration (seconds)
Required Concurrency ≈ QPS × Average Query Duration

Based on current observations, the average MRV query duration is approximately 3.5 seconds.

If we assume our effective maximum concurrency is around 100 queries (based on our Databricks serverless warehouse with up to 10 nodes and estimated parallel capacity), then:

QPS ≈ 100 ÷ 3.5 ≈ 28–29 QPS

This suggests that, under the current configuration and average duration, our realistic sustained throughput would be approximately 28–29 QPS.

To sustain 90 QPS at 3.5 seconds average duration, the required concurrency would be:

Required Concurrency ≈ 90 × 3.5 ≈ 315 concurrent queries

This would require significantly higher concurrency capacity than what we currently estimate is available.

The purpose of sharing this is to ensure we set expectations based on measurable system behavior and to enable the team to communicate performance capacity clearly and consistently. Our upcoming stress testing will validate actual concurrency limits and provide concrete data to support these discussions.

Regards,
Julio
