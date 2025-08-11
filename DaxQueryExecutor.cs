foreach (var t in Model.Tables.Where(t => t.InMemory)) {
    foreach (var c in t.Columns) {
        if (c.Aggregation != null) {
            var a = c.Aggregation;
            Print($"{t.Name}.{c.Name} | {a.Summarization} -> {a.DetailTable?.Name}.{a.DetailColumn?.Name}");
        }
    }
}
