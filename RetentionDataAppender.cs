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

    while (reader.Read())
    {
        // Detect outer array start
        if (!outerArrayStarted)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
                outerArrayStarted = true;

            continue;
        }

        // Detect row start
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            long rowStart = reader.BytesConsumed - 1; // rewind to '['

            if (TryReadRow(ref reader, out var row))
            {
                onRow(row);
                continue;
            }

            // 🚨 Row incomplete — rewind reader so bytes are not lost
            var rewindSpan = span.Slice((int)rowStart);
            reader = new Utf8JsonReader(rewindSpan, isFinal, state);

            break; // exit so caller carries remaining bytes
        }
    }

    state = reader.CurrentState;
    return (int)reader.BytesConsumed;
}



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

