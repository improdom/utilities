Hi Anupam,

Let me clarify how we’re thinking about this.

For CubIQ we still treat Event Radar as the single source of truth for activity status. The existing Azure Function that reads from the Marvel consumer group and writes into the Event Radar DB will continue to provide the status that feeds the Marvel status screens, orchestration views, and report-readiness visuals. In the interim, ARC Orchestrator is only used to drive CubIQ component execution; it will not bypass Event Radar or introduce a separate “half and half” status path. FO events continue to come from ARC, and node/readiness events are still pushed via Marvel into Event Radar and then surfaced in Marvel reports.

The reason we’re proposing to rework the current implementation is that, while it works today as a simple Marvel-consumer-to-Event-Radar writer, it doesn’t cover several behaviours we now need for CubIQ:

A more generic capability to raise/publish events from multiple components, not just the current Marvel flow.

Event aggregation based on weight/size, so very large or numerous events can be split into separate batches instead of being processed in a single iteration.

Event orchestration in an event-driven way (pushing messages to consumers instead of relying on components continuously polling).

Stronger resilience: centralised error handling, retries and failure tracking.

Because event handling is central to the platform and we have limited development time, the plan is:

keep the existing Azure Function path so that status and readiness for Event Radar/Marvel visuals remain intact,

use ARC Orchestrator to unblock CubIQ development in the short term, and

in parallel, have Mohan and Siva lead the re-implementation of this as a proper event service which will replace the current orchestration in mid-January, assuming development goes as planned
