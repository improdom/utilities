using Microsoft.Azure.Databricks.Client;
using Microsoft.Azure.Databricks.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class DatabricksTableManager
{
    private readonly IDatabricksClient _databricksClient;

    public Action<string> Logger { get; set; } = Console.WriteLine;

    private static readonly HashSet<string> RequiredColumns = new() { "COB_DATE", "QUERY_NAME" };

    public DatabricksTableManager(IDatabricksClient databricksClient)
    {
        _databricksClient = databricksClient ?? throw new ArgumentNullException(nameof(databricksClient));
    }

    public async Task CreateOrUpdateTableAsync(string databaseName, string tableName, Dictionary<string, string> columns)
    {
        string fullTableName = $"{databaseName}.{tableName}";

        EnsureRequiredColumns(columns);

        bool tableExists = await TableExistsAsync(databaseName, tableName);

        if (!tableExists)
        {
            Logger($"Table {fullTableName} does not exist. Creating...");
            string createSql = GenerateCreateTableSql(databaseName, tableName, columns);
            await ExecuteSqlAsync(createSql);
        }
        else
        {
            Logger($"Table {fullTableName} exists. Validating schema...");
            await UpdateTableSchemaAsync(databaseName, tableName, columns);
        }
    }

    public async Task AddColumnIfMissingAsync(string databaseName, string tableName, string columnName, string columnType)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentException("Column name must not be empty.", nameof(columnName));

        if (string.IsNullOrWhiteSpace(columnType))
            throw new ArgumentException("Column type must not be empty.", nameof(columnType));

        string alterSql = $"ALTER TABLE {databaseName}.{tableName} ADD COLUMNS ({columnName} {columnType})";
        Logger?.Invoke($"Adding column '{columnName}' of type '{columnType}' to {databaseName}.{tableName}...");
        await ExecuteSqlAsync(alterSql);
    }

    private void EnsureRequiredColumns(Dictionary<string, string> columns)
    {
        if (!columns.ContainsKey("COB_DATE"))
            columns["COB_DATE"] = "DATE";

        if (!columns.ContainsKey("QUERY_NAME"))
            columns["QUERY_NAME"] = "STRING";
    }

    private async Task<bool> TableExistsAsync(string databaseName, string tableName)
    {
        string sql = $"SHOW TABLES IN {databaseName} LIKE '{tableName}'";
        var result = await ExecuteSqlAsync(sql);
        return result.Contains(tableName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpdateTableSchemaAsync(string databaseName, string tableName, Dictionary<string, string> expectedColumns)
    {
        string describeSql = $"DESCRIBE TABLE {databaseName}.{tableName}";
        var describeResult = await ExecuteSqlAsync(describeSql);

        var existingColumns = describeResult.Split('\n')
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2)
            .Select(parts => new { Name = parts[0], Type = parts[1] })
            .ToDictionary(x => x.Name, x => x.Type, StringComparer.OrdinalIgnoreCase);

        var columnsToAdd = expectedColumns.Keys.Except(existingColumns.Keys).ToList();

        foreach (var column in columnsToAdd)
        {
            string alterSql = $"ALTER TABLE {databaseName}.{tableName} ADD COLUMNS ({column} {expectedColumns[column]})";
            Logger($"Adding column {column} to {databaseName}.{tableName}...");
            await ExecuteSqlAsync(alterSql);
        }
    }

    private string GenerateCreateTableSql(string databaseName, string tableName, Dictionary<string, string> columns)
    {
        string columnsSql = string.Join(", ", columns.Select(c => $"{c.Key} {c.Value}"));
        return $"CREATE TABLE IF NOT EXISTS {databaseName}.{tableName} ({columnsSql}) USING DELTA PARTITIONED BY (COB_DATE, QUERY_NAME)";
    }

    private async Task<string> ExecuteSqlAsync(string sql)
    {
        var command = new Command
        {
            Language = CommandLanguage.SQL,
            CommandText = sql
        };

        var result = await _databricksClient.Command.Execute(command);
        return result?.Result ?? string.Empty;
    }
}
