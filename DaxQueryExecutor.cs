foreach (var item in items)
{
    var line = "\"" + item.ColumnName + "\",\"" + item.TableName + "\",\"" + item.DataType + "\",\"" + item.InMemory + "\"";
    csv.AppendLine(line);
}
