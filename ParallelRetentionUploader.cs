public static async Task StreamParseJsonArrayOfRowsAsync(
    Stream stream,
    Action<object?[]> onRow,
    CancellationToken ct)
{
    byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
    try
    {
        var state = new JsonReaderState(new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow
        });

        int bytesInBuffer = 0;
        bool isFinal = false;
        bool outerArrayStarted = false;

        while (!isFinal)
        {
            ct.ThrowIfCancellationRequested();

            // Read after any carry-over bytes
            int read = await stream.ReadAsync(buffer, bytesInBuffer, buffer.Length - bytesInBuffer, ct)
                                   .ConfigureAwait(false);

            if (read == 0)
                isFinal = true;

            int totalBytes = bytesInBuffer + read;

            // Parse using sync method (Span/ref structs live ONLY there)
            int bytesConsumed = ParseSpanFromBuffer(
                buffer,
                totalBytes,
                isFinal,
                ref state,
                ref outerArrayStarted,
                onRow);

            // Carry over unconsumed bytes (partial JSON token)
            int remaining = totalBytes - bytesConsumed;
            if (remaining > 0)
            {
                Buffer.BlockCopy(buffer, bytesConsumed, buffer, 0, remaining);
            }

            bytesInBuffer = remaining;
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}



private static int ParseSpanFromBuffer(
    byte[] buffer,
    int length,
    bool isFinal,
    ref JsonReaderState state,
    ref bool outerArrayStarted,
    Action<object?[]> onRow)
{
    var span = new ReadOnlySpan<byte>(buffer, 0, length); // OK: sync method
    var reader = new Utf8JsonReader(span, isFinal, state);

    while (reader.Read())
    {
        if (!outerArrayStarted)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
                outerArrayStarted = true;

            continue;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            if (TryReadRow(ref reader, out var row))
                onRow(row);
        }
    }

    state = reader.CurrentState;
    return (int)reader.BytesConsumed;
}
