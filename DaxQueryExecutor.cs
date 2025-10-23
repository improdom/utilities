foreach (var table in Model.Tables)
{
    foreach (var column in table.Columns)
    {
        // Skip measure or calculated columns
        if (column.Type != ColumnType.Data)
            continue;

        // Check if column is used in any relationship
        bool isInRelationship = Model.Relationships.Any(r =>
            (r.FromColumn == column) || (r.ToColumn == column));

        // Hide the column if it's part of a relationship
        if (isInRelationship)
            column.IsHidden = true;
    }
}
