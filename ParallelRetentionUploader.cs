using System.Text.RegularExpressions;
using Microsoft.AnalysisServices.Tabular;

namespace MrvBuilder.MetricsViews;

public sealed class AttributeMappingRow
{
    public required string TableName { get; init; }
    public required string AttributeName { get; init; }
    public required string DataType { get; init; }
    public string? SourceTableName { get; init; }
    public string? SourceColumnName { get; init; }
}


public sealed class PowerBiSemanticModelAttributeMappingRepository
{
    private readonly string _xmlaConnectionString;
    private readonly string? _databaseName;

    public PowerBiSemanticModelAttributeMappingRepository(string xmlaConnectionString, string? databaseName = null)
    {
        _xmlaConnectionString = xmlaConnectionString;
        _databaseName = databaseName;
    }

    public Task<List<AttributeMappingRow>> LoadAsync()
    {
        var results = new List<AttributeMappingRow>();

        using var server = new Server();
        server.Connect(_xmlaConnectionString);

        var database = ResolveDatabase(server);
        var model = database.Model ?? throw new InvalidOperationException("The semantic model database has no Tabular model.");

        foreach (var table in model.Tables)
        {
            var sourceTableName = TryGetSourceTableName(table);

            foreach (var column in table.Columns)
            {
                results.Add(new AttributeMappingRow
                {
                    TableName = table.Name,
                    AttributeName = column.Name,
                    DataType = column.DataType.ToString(),
                    SourceTableName = sourceTableName,
                    SourceColumnName = TryGetSourceColumnName(column)
                });
            }
        }

        return Task.FromResult(results);
    }

    private Database ResolveDatabase(Server server)
    {
        if (!string.IsNullOrWhiteSpace(_databaseName))
        {
            var named = server.Databases.Cast<Database>()
                .FirstOrDefault(d => string.Equals(d.Name, _databaseName, StringComparison.OrdinalIgnoreCase));

            if (named is null)
                throw new InvalidOperationException($"Semantic model database '{_databaseName}' was not found on the XMLA endpoint.");

            return named;
        }

        if (server.Databases.Count == 0)
            throw new InvalidOperationException("No semantic model databases were found on the XMLA endpoint.");

        if (server.Databases.Count > 1)
        {
            throw new InvalidOperationException(
                "Multiple semantic model databases were found. Specify the database name when creating the repository.");
        }

        return server.Databases.Cast<Database>().Single();
    }

    private static string? TryGetSourceColumnName(Column column)
    {
        if (column is DataColumn dataColumn && !string.IsNullOrWhiteSpace(dataColumn.SourceColumn))
            return dataColumn.SourceColumn;

        return null;
    }

    private static string? TryGetSourceTableName(Table table)
    {
        foreach (var partition in table.Partitions)
        {
            var source = partition.Source;
            if (source is null) continue;

            var fromEntity = TryReadStringProperty(source, "EntityName");
            if (!string.IsNullOrWhiteSpace(fromEntity))
                return fromEntity;

            var expression = TryReadStringProperty(source, "Expression");
            var fromM = TryExtractTableFromM(expression);
            if (!string.IsNullOrWhiteSpace(fromM))
                return fromM;

            var query = TryReadStringProperty(source, "Query");
            var fromSql = TryExtractTableFromSql(query);
            if (!string.IsNullOrWhiteSpace(fromSql))
                return fromSql;
        }

        return null;
    }

    private static string? TryReadStringProperty(object instance, string propertyName)
    {
        var prop = instance.GetType().GetProperty(propertyName);
        if (prop is null || prop.PropertyType != typeof(string))
            return null;

        return prop.GetValue(instance) as string;
    }

    private static string? TryExtractTableFromM(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        // Common Power Query navigation pattern: Source{[Schema="dbo",Item="Table"]}[Data]
        var schemaMatch = Regex.Match(expression, @"Schema\s*=\s*""(?<schema>[^""]+)""", RegexOptions.IgnoreCase);
        var itemMatch = Regex.Match(expression, @"Item\s*=\s*""(?<item>[^""]+)""", RegexOptions.IgnoreCase);
        if (itemMatch.Success)
        {
            var item = itemMatch.Groups["item"].Value;
            if (schemaMatch.Success)
                return $"{schemaMatch.Groups["schema"].Value}.{item}";
            return item;
        }

        // Fallback for direct entity references like #"FactSales"
        var entityMatch = Regex.Match(expression, @"#""(?<entity>[^""]+)""");
        return entityMatch.Success ? entityMatch.Groups["entity"].Value : null;
    }

    private static string? TryExtractTableFromSql(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        // Best-effort parse of the first FROM target.
        var match = Regex.Match(
            sql,
            @"\bFROM\s+(?<table>(?:\[[^\]]+\]|""[^""]+""|\w+)(?:\s*\.\s*(?:\[[^\]]+\]|""[^""]+""|\w+))?)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return null;

        var raw = match.Groups["table"].Value;
        return raw.Replace("[", string.Empty)
                  .Replace("]", string.Empty)
                  .Replace("\"", string.Empty)
                  .Replace(" ", string.Empty);
    }
}
