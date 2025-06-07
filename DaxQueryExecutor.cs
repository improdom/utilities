public class QueryReadinessStatusDto
{
    public string QuerySpace { get; set; }
    public long QueryId { get; set; }
    public string QueryName { get; set; }
    public string QueryStatus { get; set; }
    public int TotalNodes { get; set; }
    public int ReadyNodes { get; set; }
    public int PendingNodes { get; set; }
}
