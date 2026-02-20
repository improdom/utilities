using System.Text.Json;

private static int ParseSpanFromBuffer(
    byte[] buffer,
    int length,
    bool isFinal,
    ref JsonReaderState state,
    ref bool outerArrayStarted,
    Action<object?[]> onRow)
{
    var span = new ReadOnlySpan<byte>(buffer, 0, length);
    var reader = new Utf8JsonReader(span, isFinal, state);

    while (true)
    {
        try
        {
            while (reader.Read())
            {
                // Wait for the outer array '['
                if (!outerArrayStarted)
                {
                    if (reader.TokenType == JsonTokenType.StartArray)
                        outerArrayStarted = true;

                    continue;
                }

                // Each row is itself an array: [col1, col2, ...]
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    int rowStart = checked((int)reader.TokenStartIndex);

                    if (TryReadRow(ref reader, out var row))
                    {
                        onRow(row);
                        continue;
                    }

                    // Row is incomplete (split across buffers): rewind to row start
                    reader = new Utf8JsonReader(span.Slice(rowStart), isFinal, state);
                    state = reader.CurrentState;
                    return rowStart; // consumed up to rowStart; caller will carry remaining bytes
                }

                // Optional: if the outer array ends, we can stop parsing this span
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    state = reader.CurrentState;
                    return (int)reader.BytesConsumed;
                }
            }

            // No more tokens in this span
            state = reader.CurrentState;
            return (int)reader.BytesConsumed;
        }
        catch (JsonException) when (!isFinal)
        {
            // IMPORTANT: On non-final buffers, JsonException often means the JSON value
            // was split across buffers. We must NOT drop bytes; just stop and carry remainder.
            state = reader.CurrentState;
            return (int)reader.BytesConsumed;
        }
    }
}

private static bool TryReadRow(ref Utf8JsonReader reader, out object?[] row)
{
    // We enter positioned on StartArray (row)
    var values = new List<object?>(16);

    while (true)
    {
        if (!reader.Read())
        {
            row = null!;
            return false; // incomplete row
        }

        if (reader.TokenType == JsonTokenType.EndArray)
        {
            row = values.ToArray();
            return true;
        }

        // Parse one value (primitive or complex)
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                values.Add(null);
                break;

            case JsonTokenType.True:
                values.Add(true);
                break;

            case JsonTokenType.False:
                values.Add(false);
                break;

            case JsonTokenType.String:
                values.Add(reader.GetString());
                break;

            case JsonTokenType.Number:
                if (reader.TryGetInt64(out var l)) values.Add(l);
                else if (reader.TryGetDouble(out var d)) values.Add(d);
                else values.Add(reader.GetDecimal());
                break;

            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                // Complex value inside the row: parse as JsonElement.
                // This advances the reader to the end of that value.
                using (var doc = JsonDocument.ParseValue(ref reader))
                {
                    values.Add(doc.RootElement.Clone());
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unexpected token {reader.TokenType} inside row array.");
        }
    }
}
