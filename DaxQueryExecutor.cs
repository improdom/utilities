var existing = await (
    from querySpace in retentionSqlDbContext.QuerySpaces
    where querySpace != null
    let execStatusList = querySpace.QuerySpaceExecutionStatuses
    where execStatusList != null
    let execStatus = execStatusList
        .Where(e => e.QuerySpaceId == querySpace.Id && e.CoBDate == coBDate.Date)
        .FirstOrDefault()
    where execStatus != null
    select new { querySpace, execStatus }
).FirstOrDefaultAsync();
