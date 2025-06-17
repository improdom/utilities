// Generate CSV header
var csv = new System.Text.StringBuilder();
csv.AppendLine("ColumnName,TableName,DataType,InMemory");

// Generate CSV rows
foreach (var item in items)
{
    var line = $"\"{item.ColumnName}\",\"{item.TableName}\",\"{item.DataType}\",\"{item.InMemory}\"";
    csv.AppendLine(line);
}

// Output to Tabular Editor's output window
Output(csv.ToString());
