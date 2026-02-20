private static bool TryReadRow(ref Utf8JsonReader reader, out object?[] row)
{
    var values = new List<object?>(16);

    while (true)
    {
        if (!reader.Read())
        {
            row = null!;
            return false; // incomplete row
        }

        switch (reader.TokenType)
        {
            case JsonTokenType.EndArray:
                row = values.ToArray();
                return true;

            case JsonTokenType.String:
                values.Add(reader.GetString());
                break;

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l))
                    values.Add(l);
                else if (reader.TryGetDouble(out var d))
                    values.Add(d);
                else
                    values.Add(reader.GetDecimal());
                break;

            case JsonTokenType.True:
                values.Add(true);
                break;

            case JsonTokenType.False:
                values.Add(false);
                break;

            case JsonTokenType.Null:
                values.Add(null);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unexpected token {reader.TokenType} inside row array");
        }
    }
}
