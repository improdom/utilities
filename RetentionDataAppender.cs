using Microsoft.Azure.Databricks.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class RetentionDataAppender
{
    private readonly IDatabricksClient _databricksClient;
    private readonly DatabricksTableManager _tableManager;
    private const int DefaultBatchSize = 1000;

    public Action<string> Logger { get; set; } = Console.WriteLine;

    public RetentionDataAppender(IDatabricksClient databricksClient, DatabricksTableManager tableManager)
    {
        _databricksClient = databricksClient ?? throw new ArgumentNullException(nameof(databricksClient));
        _tableManager = tableManager ?? throw new ArgumentNullException(nameof(tableManager));
    }

    public async Task AppendDataAsync(string databaseName, string tableName, DataTable data, int? batchSize = null)
    {
        if (data == null || data.Rows.Count == 0)
            throw new ArgumentException("DataTable must not be null or empty.", nameof(data));

        await ValidateSchemaAsync(databaseName, tableName, data);

        Logger($"Inserting data into table {tableName}...");
        if (batchSize.HasValue && batchSize.Value > 0)
        {
            Logger($"Batching enabled (Batch Size: {batchSize.Value})");
            foreach (var batch in SplitIntoBatches(data, batchSize.Value))
            {
                string batchInsertSql = GenerateBatchInsertSql(databaseName, tableName, batch);
                await ExecuteSqlAsync(batchInsertSql);
                Logger($"Batch inserted successfully (Batch Size: {batch.Count()})");
            }
        }
        else
        {
            Logger("Batching disabled. Inserting all data in a single batch.");
            string singleInsertSql = GenerateBatchInsertSql(databaseName, tableName, data.Rows.Cast<DataRow>());
            await ExecuteSqlAsync(singleInsertSql);
            Logger($"Single batch inserted successfully (Row Count: {data.Rows.Count})");
        }

        Logger("Data appended successfully.");
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        var command = new Command
        {
            Language = CommandLanguage.SQL,
            CommandText = sql
        };

        await _databricksClient.Command.Execute(command);
    }

    private async Task ValidateSchemaAsync(string databaseName, string tableName, DataTable data)
    {
        Logger($"Validating schema for {databaseName}.{tableName}...");

        string describeSql = $"DESCRIBE TABLE {databaseName}.{tableName}";
        var command = new Command
        {
            Language = CommandLanguage.SQL,
            CommandText = describeSql
        };

        var describeResult = await _databricksClient.Command.Execute(command);

        var targetColumnsInfo = describeResult.Result.Split('\n')
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2)
            .Select(parts => parts[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (DataColumn incomingColumn in data.Columns)
        {
            if (!targetColumnsInfo.Contains(incomingColumn.ColumnName))
            {
                Logger($"Column '{incomingColumn.ColumnName}' does not exist. Delegating to TableManager to add it...");
                await _tableManager.AddColumnIfMissingAsync(databaseName, tableName, incomingColumn.ColumnName, MapType(incomingColumn.DataType));
            }
        }

        Logger($"Schema validation completed successfully for {databaseName}.{tableName}.");
    }

    private string GenerateBatchInsertSql(string databaseName, string tableName, IEnumerable<DataRow> rows)
    {
        var valuesList = new List<string>();

        foreach (var row in rows)
        {
            var values = new List<string>();

            foreach (DataColumn column in row.Table.Columns)
            {
                var value = row[column];
                if (value == DBNull.Value)
                {
                    values.Add("NULL");
                }
                else if (column.DataType == typeof(string) || column.DataType == typeof(DateTime))
                {
                    values.Add($"'{value.ToString().Replace("'", "''")}'");
                }
                else
                {
                    values.Add(value.ToString());
                }
            }

            valuesList.Add($"({string.Join(", ", values)})");
        }

        return $"INSERT INTO {databaseName}.{tableName} VALUES {string.Join(", ", valuesList)}";
    }

    private IEnumerable<IEnumerable<DataRow>> SplitIntoBatches(DataTable dataTable, int batchSize)
    {
        var batch = new List<DataRow>(batchSize);

        foreach (DataRow row in dataTable.Rows)
        {
            batch.Add(row);
            if (batch.Count == batchSize)
            {
                yield return batch;
                batch = new List<DataRow>(batchSize);
            }
        }

        if (batch.Count > 0)
        {
            yield return batch;
        }
    }

    private string MapType(Type type)
    {
        if (type == typeof(string)) return "STRING";
        if (type == typeof(int) || type == typeof(long)) return "BIGINT";
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "DOUBLE";
        if (type == typeof(bool)) return "BOOLEAN";
        if (type == typeof(DateTime)) return "TIMESTAMP";

        throw new NotSupportedException($"Unsupported .NET type: {type.Name}");
    }
}
