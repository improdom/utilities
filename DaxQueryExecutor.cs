var existingQuerySpace = await retentionSqlDbContext.QuerySpaces
    .FirstOrDefaultAsync(q => q.Name == querySpaceName);

QuerySpaceExecutionStatus? existingQueryStatus = null;

if (existingQuerySpace != null)
{
    existingQueryStatus = await retentionSqlDbContext.QuerySpaceExecutionStatuses
        .FirstOrDefaultAsync(x =>
            x.QuerySpaceId == existingQuerySpace.Id &&
            x.CoBDate == coBDate.Date);
}

if (existingQueryStatus == null)
{
    retentionSqlDbContext.QuerySpaceExecutionStatuses.Add(new QuerySpaceExecutionStatus
    {
        QuerySpaceId = existingQuerySpace.Id,
        CoBDate = coBDate.Date,
        Status = executionStatus.ToString(),
        CreatedAt = DateTime.UtcNow,
        LastUpdated = DateTime.UtcNow,
        ReadyEventPublished = readyEventRaised
    });
}
else
{
    existingQueryStatus.CoBDate = coBDate;
    existingQueryStatus.Status = executionStatus.ToString();
    existingQueryStatus.LastUpdated = DateTime.UtcNow;
    existingQueryStatus.ReadyEventPublished = readyEventRaised;
}

await retentionSqlDbContext.SaveChangesAsync();
