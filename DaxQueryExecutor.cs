using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

public class DaxQueryExecutor
{
    private readonly string _connectionString;

    public Action<string> Logger { get; set; } = Console.WriteLine;

    public DaxQueryExecutor(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<AdomdDataReader> ExecuteDaxQueryAsync(string daxQuery)
    {
        if (string.IsNullOrWhiteSpace(daxQuery))
            throw new ArgumentException("DAX query must not be null or empty.", nameof(daxQuery));

        var connection = new AdomdConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            using (var command = connection.CreateCommand())
            {
                Logger($"Executing DAX query: {daxQuery}");
                command.CommandText = daxQuery;
                command.CommandType = CommandType.Text;
                return await Task.FromResult(command.ExecuteReader());
            }
        }
        catch (Exception ex)
        {
            Logger($"Error executing DAX query: {ex.Message}");
            connection.Dispose();
            throw;
        }
    }

    public async Task<object> ExecuteScalarDaxQueryAsync(string daxQuery)
    {
        if (string.IsNullOrWhiteSpace(daxQuery))
            throw new ArgumentException("DAX query must not be null or empty.", nameof(daxQuery));

        var connection = new AdomdConnection(_connectionString);
        await connection.OpenAsync();

        try
        {
            using (var command = connection.CreateCommand())
            {
                Logger($"Executing scalar DAX query: {daxQuery}");
                command.CommandText = daxQuery;
                command.CommandType = CommandType.Text;
                return await Task.FromResult(command.ExecuteScalar());
            }
        }
        catch (Exception ex)
        {
            Logger($"Error executing scalar DAX query: {ex.Message}");
            connection.Dispose();
            throw;
        }
    }

    public async Task<Dictionary<string, DataTable>> ExecuteBatchDaxQueriesAsync(Dictionary<string, string> queries)
    {
        if (queries == null || queries.Count == 0)
            throw new ArgumentException("Query batch must not be null or empty.", nameof(queries));

        var results = new Dictionary<string, DataTable>();
        using (var connection = new AdomdConnection(_connectionString))
        {
            await connection.OpenAsync();

            foreach (var kvp in queries)
            {
                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        Logger($"Executing batch DAX query: {kvp.Key}");
                        command.CommandText = kvp.Value;
                        command.CommandType = CommandType.Text;

                        using (var reader = command.ExecuteReader())
                        {
                            var dataTable = new DataTable();
                            dataTable.Load(reader);
                            results[kvp.Key] = dataTable;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger($"Failed executing query '{kvp.Key}': {ex.Message}");
                    results[kvp.Key] = null;
                }
            }
        }
        return results;
    }
}
