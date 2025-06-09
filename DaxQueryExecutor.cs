public async Task<List<QueryReadinessStatusDto>> GetReadinessSummaryAsync(DateTime cobDate)
{
    return await (
        from q in _context.QueryReadinesses
        join qs in _context.QuerySpaces on q.QuerySpaceName equals qs.Name
        join e in _context.QueryExecutionStatuses
            on new { q.QueryId, CobDate = cobDate } equals new { e.QueryId, e.CobDate } into execJoin
        from ge in execJoin.DefaultIfEmpty()

        let readiness = _context.ReadinessStatuses
            .Where(r => r.QueryId == q.QueryId && r.CobDate == cobDate)

        select new QueryReadinessStatusDto
        {
            QuerySpace = qs.Name,
            QueryId = q.QueryId,
            QueryName = q.QueryName,
            QueryStatus = ge.Status ?? "Pending",
            TotalNodes = readiness.Count(),
            ReadyNodes = readiness.Count(r => r.IsReady),
            PendingNodes = readiness.Count(r => !r.IsReady)
        }).ToListAsync();
}
