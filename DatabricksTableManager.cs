using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBiMetricViewYaml
{
    /// <summary>
    /// Inspects a Power BI semantic model through XMLA/TOM and generates
    /// a Databricks metric-view YAML definition with:
    /// - version
    /// - source
    /// - joins
    /// - dimensions
    /// - comment
    ///
    /// No measures are emitted.
    /// </summary>
    public sealed class SemanticModelMetricViewYamlGenerator
    {
        // Optional annotation names you can place on tables/columns in the semantic model.
        public string DatabricksTableAnnotationName { get; set; } = "Databricks.SourceTable";
        public string DatabricksColumnAnnotationName { get; set; } = "Databricks.SourceColumn";
        public string FriendlyNameAnnotationName { get; set; } = "MetricView.FriendlyName";
        public string DescriptionAnnotationName { get; set; } = "MetricView.Description";
        public string RoleAnnotationName { get; set; } = "MetricView.Role"; // "Fact" / "Dimension"

        public string DefaultCatalog { get; set; } = "main";
        public string DefaultSchema { get; set; } = "default";
        public string YamlVersion { get; set; } = "1.1";

        public bool PrefixDuplicateDimensionNamesWithTable { get; set; } = true;
        public bool PrefixAllDimensionNamesWithTable { get; set; } = false;
        public bool IncludeFactColumnsThatLookLikeDimensions { get; set; } = true;
        public bool IncludeHiddenColumns { get; set; } = false;

        /// <summary>
        /// Generates dimensions-only YAML for a fact table in a Power BI semantic model.
        /// </summary>
        /// <param name="xmlaConnectionStringOrPowerBiServer">
        /// Example: powerbi://api.powerbi.com/v1.0/myorg/MyWorkspace
        /// </param>
        /// <param name="databaseName">Semantic model name in the workspace.</param>
        /// <param name="factTableName">Fact table to use as the metric-view source.</param>
        /// <returns>YAML text.</returns>
        public string GenerateYaml(string xmlaConnectionStringOrPowerBiServer, string databaseName, string factTableName)
        {
            if (string.IsNullOrWhiteSpace(xmlaConnectionStringOrPowerBiServer))
                throw new ArgumentException("XMLA server/connection string is required.", nameof(xmlaConnectionStringOrPowerBiServer));
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("Database name is required.", nameof(databaseName));
            if (string.IsNullOrWhiteSpace(factTableName))
                throw new ArgumentException("Fact table name is required.", nameof(factTableName));

            using var server = new Server();
            server.Connect(xmlaConnectionStringOrPowerBiServer);

            try
            {
                var database = server.Databases
                    .Cast<Database>()
                    .FirstOrDefault(d => string.Equals(d.Name, databaseName, StringComparison.OrdinalIgnoreCase));

                if (database == null)
                    throw new InvalidOperationException($"Semantic model '{databaseName}' was not found.");

                if (database.Model == null)
                    throw new InvalidOperationException($"Semantic model '{databaseName}' does not have a tabular model.");

                return GenerateYaml(database.Model, factTableName);
            }
            finally
            {
                server.Disconnect();
            }
        }

        /// <summary>
        /// Generates YAML directly from a TOM Model.
        /// </summary>
        public string GenerateYaml(Model model, string factTableName)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var tables = ReadTables(model);
            var relationships = ReadRelationships(model);

            var factTable = tables.FirstOrDefault(t =>
                string.Equals(t.Name, factTableName, StringComparison.OrdinalIgnoreCase));

            if (factTable == null)
                throw new InvalidOperationException($"Fact table '{factTableName}' was not found.");

            var relatedDimensions = GetRelatedDimensionTables(factTable, tables, relationships);
            var nameRegistry = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var yaml = new StringBuilder();

            AppendLine(yaml, $"version: {YamlEscapeScalar(YamlVersion)}");
            if (!string.IsNullOrWhiteSpace(factTable.Description))
                AppendLine(yaml, $"comment: {YamlEscapeScalar(factTable.Description)}");
            else
                AppendLine(yaml, $"comment: {YamlEscapeScalar($"Generated from semantic model for fact table {factTable.FriendlyName}")}");

            AppendLine(yaml, $"source: {YamlEscapeScalar(GetSourceTableName(factTable))}");

            if (relatedDimensions.Count > 0)
            {
                AppendLine(yaml, "joins:");
                foreach (var dimTable in relatedDimensions)
                {
                    var relationship = FindRelationshipBetween(factTable, dimTable, relationships);
                    if (relationship == null)
                        continue;

                    AppendLine(yaml, $"  - name: {YamlEscapeScalar(dimTable.Name)}");
                    AppendLine(yaml, $"    source: {YamlEscapeScalar(GetSourceTableName(dimTable))}");
                    AppendLine(yaml, $"    on: {YamlEscapeScalar(BuildJoinExpression(factTable, dimTable, relationship))}");
                }
            }

            var allDimensions = new List<YamlDimension>();

            foreach (var dimTable in relatedDimensions)
            {
                foreach (var column in dimTable.Columns
                             .Where(c => IncludeHiddenColumns || !c.IsHidden))
                {
                    var preferredName = PrefixAllDimensionNamesWithTable
                        ? $"{dimTable.FriendlyName} {column.FriendlyName}"
                        : column.FriendlyName;

                    var finalName = ReserveUniqueName(
                        preferredName,
                        dimTable.FriendlyName,
                        nameRegistry,
                        PrefixDuplicateDimensionNamesWithTable);

                    allDimensions.Add(new YamlDimension
                    {
                        Name = finalName,
                        Expr = $"{dimTable.Name}.{GetPhysicalColumnName(column)}",
                        Comment = column.Description
                    });
                }
            }

            if (IncludeFactColumnsThatLookLikeDimensions)
            {
                foreach (var column in factTable.Columns
                             .Where(c => (IncludeHiddenColumns || !c.IsHidden) && LooksDimensionLike(c)))
                {
                    var preferredName = PrefixAllDimensionNamesWithTable
                        ? $"{factTable.FriendlyName} {column.FriendlyName}"
                        : column.FriendlyName;

                    var finalName = ReserveUniqueName(
                        preferredName,
                        factTable.FriendlyName,
                        nameRegistry,
                        PrefixDuplicateDimensionNamesWithTable);

                    allDimensions.Add(new YamlDimension
                    {
                        Name = finalName,
                        Expr = GetPhysicalColumnName(column),
                        Comment = column.Description
                    });
                }
            }

            if (allDimensions.Count > 0)
            {
                AppendLine(yaml, "dimensions:");
                foreach (var dimension in allDimensions.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    AppendLine(yaml, $"  - name: {YamlEscapeScalar(dimension.Name)}");
                    AppendLine(yaml, $"    expr: {YamlEscapeScalar(dimension.Expr)}");
                    if (!string.IsNullOrWhiteSpace(dimension.Comment))
                        AppendLine(yaml, $"    comment: {YamlEscapeScalar(dimension.Comment!)}");
                }
            }

            return yaml.ToString();
        }

        /// <summary>
        /// Optional helper to save the generated YAML to a file.
        /// </summary>
        public void GenerateYamlToFile(string xmlaConnectionStringOrPowerBiServer, string databaseName, string factTableName, string outputFilePath)
        {
            var yaml = GenerateYaml(xmlaConnectionStringOrPowerBiServer, databaseName, factTableName);
            File.WriteAllText(outputFilePath, yaml, Encoding.UTF8);
        }

        private List<TableInfo> ReadTables(Model model)
        {
            var result = new List<TableInfo>();

            foreach (var table in model.Tables)
            {
                var tableInfo = new TableInfo
                {
                    Name = table.Name,
                    FriendlyName = GetFriendlyName(table) ?? table.Name,
                    Description = GetDescription(table),
                    IsHidden = table.IsHidden,
                    IsFact = string.Equals(GetAnnotation(table, RoleAnnotationName), "Fact", StringComparison.OrdinalIgnoreCase),
                    SourceTableName = GetAnnotation(table, DatabricksTableAnnotationName)
                };

                foreach (var column in table.Columns.OfType<DataColumn>())
                {
                    tableInfo.Columns.Add(new ColumnInfo
                    {
                        TableName = table.Name,
                        Name = column.Name,
                        FriendlyName = GetFriendlyName(column) ?? column.Name,
                        Description = GetDescription(column),
                        IsHidden = column.IsHidden,
                        DataType = column.DataType.ToString(),
                        SourceColumnName = GetAnnotation(column, DatabricksColumnAnnotationName)
                    });
                }

                result.Add(tableInfo);
            }

            return result;
        }

        private static List<RelationshipInfo> ReadRelationships(Model model)
        {
            var result = new List<RelationshipInfo>();

            foreach (var rel in model.Relationships.OfType<SingleColumnRelationship>())
            {
                result.Add(new RelationshipInfo
                {
                    FromTable = rel.FromTable.Name,
                    FromColumn = rel.FromColumn.Name,
                    ToTable = rel.ToTable.Name,
                    ToColumn = rel.ToColumn.Name,
                    IsActive = rel.IsActive
                });
            }

            return result;
        }

        private List<TableInfo> GetRelatedDimensionTables(TableInfo factTable, List<TableInfo> allTables, List<RelationshipInfo> relationships)
        {
            var relatedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rel in relationships.Where(r => r.IsActive))
            {
                if (string.Equals(rel.FromTable, factTable.Name, StringComparison.OrdinalIgnoreCase))
                    relatedNames.Add(rel.ToTable);
                else if (string.Equals(rel.ToTable, factTable.Name, StringComparison.OrdinalIgnoreCase))
                    relatedNames.Add(rel.FromTable);
            }

            return allTables
                .Where(t => !string.Equals(t.Name, factTable.Name, StringComparison.OrdinalIgnoreCase))
                .Where(t => relatedNames.Contains(t.Name))
                .Where(t => !t.IsFact) // dimensions only
                .OrderBy(t => t.FriendlyName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static RelationshipInfo? FindRelationshipBetween(TableInfo factTable, TableInfo dimTable, List<RelationshipInfo> relationships)
        {
            return relationships.FirstOrDefault(r =>
                r.IsActive &&
                (
                    (string.Equals(r.FromTable, factTable.Name, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(r.ToTable, dimTable.Name, StringComparison.OrdinalIgnoreCase))
                    ||
                    (string.Equals(r.ToTable, factTable.Name, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(r.FromTable, dimTable.Name, StringComparison.OrdinalIgnoreCase))
                ));
        }

        private string GetSourceTableName(TableInfo table)
        {
            if (!string.IsNullOrWhiteSpace(table.SourceTableName))
                return table.SourceTableName!;

            return $"{DefaultCatalog}.{DefaultSchema}.{table.Name}";
        }

        private static string GetPhysicalColumnName(ColumnInfo column)
        {
            return !string.IsNullOrWhiteSpace(column.SourceColumnName)
                ? column.SourceColumnName!
                : column.Name;
        }

        private static string BuildJoinExpression(TableInfo factTable, TableInfo dimTable, RelationshipInfo rel)
        {
            if (string.Equals(rel.FromTable, factTable.Name, StringComparison.OrdinalIgnoreCase))
                return $"source.{rel.FromColumn} = {dimTable.Name}.{rel.ToColumn}";

            return $"source.{rel.ToColumn} = {dimTable.Name}.{rel.FromColumn}";
        }

        private static bool LooksDimensionLike(ColumnInfo column)
        {
            if (column == null) return false;

            return column.DataType.Contains("String", StringComparison.OrdinalIgnoreCase)
                   || column.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
                   || column.Name.EndsWith("Key", StringComparison.OrdinalIgnoreCase)
                   || column.Name.Contains("Date", StringComparison.OrdinalIgnoreCase)
                   || column.Name.Contains("Type", StringComparison.OrdinalIgnoreCase)
                   || column.Name.Contains("Name", StringComparison.OrdinalIgnoreCase)
                   || column.Name.Contains("Code", StringComparison.OrdinalIgnoreCase);
        }

        private string? GetFriendlyName(MetadataObject obj)
        {
            return GetAnnotation(obj, FriendlyNameAnnotationName);
        }

        private string? GetDescription(MetadataObject obj)
        {
            var ann = GetAnnotation(obj, DescriptionAnnotationName);
            if (!string.IsNullOrWhiteSpace(ann))
                return ann;

            return obj switch
            {
                Table t => t.Description,
                Column c => c.Description,
                Measure m => m.Description,
                _ => null
            };
        }

        private static string? GetAnnotation(MetadataObject obj, string annotationName)
        {
            return obj.Annotations.Find(annotationName)?.Value;
        }

        private static string ReserveUniqueName(string preferredName, string tableFriendlyName, HashSet<string> usedNames, bool prefixDuplicatesWithTable)
        {
            string Normalize(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    value = "Unnamed";

                value = value.Trim();
                value = value.Replace("_", " ");
                value = Regex.Replace(value, @"\s+", " ");
                value = Regex.Replace(value, @"[^\w\s\-]", "");
                value = Regex.Replace(value, @"\s+", " ").Trim();

                if (value.Length > 120)
                    value = value.Substring(0, 120).Trim();

                return value;
            }

            var candidate = Normalize(preferredName);

            if (!usedNames.Contains(candidate))
            {
                usedNames.Add(candidate);
                return candidate;
            }

            if (prefixDuplicatesWithTable)
            {
                var prefixed = Normalize($"{tableFriendlyName} {preferredName}");
                if (!usedNames.Contains(prefixed))
                {
                    usedNames.Add(prefixed);
                    return prefixed;
                }
            }

            int suffix = 2;
            while (true)
            {
                var next = Normalize($"{preferredName} {suffix}");
                if (!usedNames.Contains(next))
                {
                    usedNames.Add(next);
                    return next;
                }
                suffix++;
            }
        }

        private static string YamlEscapeScalar(string value)
        {
            if (value == null) return "\"\"";

            // Always quote for safety.
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n") + "\"";
        }

        private static void AppendLine(StringBuilder sb, string text)
        {
            sb.AppendLine(text);
        }

        private sealed class TableInfo
        {
            public string Name { get; set; } = string.Empty;
            public string FriendlyName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsHidden { get; set; }
            public bool IsFact { get; set; }
            public string? SourceTableName { get; set; }
            public List<ColumnInfo> Columns { get; } = new();
        }

        private sealed class ColumnInfo
        {
            public string TableName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string FriendlyName { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool IsHidden { get; set; }
            public string DataType { get; set; } = string.Empty;
            public string? SourceColumnName { get; set; }
        }

        private sealed class RelationshipInfo
        {
            public string FromTable { get; set; } = string.Empty;
            public string FromColumn { get; set; } = string.Empty;
            public string ToTable { get; set; } = string.Empty;
            public string ToColumn { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        private sealed class YamlDimension
        {
            public string Name { get; set; } = string.Empty;
            public string Expr { get; set; } = string.Empty;
            public string? Comment { get; set; }
        }
    }
}




var generator = new SemanticModelMetricViewYamlGenerator
{
    DefaultCatalog = "main",
    DefaultSchema = "risk",
    PrefixDuplicateDimensionNamesWithTable = true,
    PrefixAllDimensionNamesWithTable = false,
    IncludeFactColumnsThatLookLikeDimensions = true
};

string yaml = generator.GenerateYaml(
    "powerbi://api.powerbi.com/v1.0/myorg/MyWorkspace",
    "My Semantic Model",
    "FactPositions");

Console.WriteLine(yaml);
