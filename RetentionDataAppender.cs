public static async Task StreamParseJsonArrayOfRowsAsync(
    Stream stream,
    Action<object?[]> onRow,
    CancellationToken ct)
{
    // Start with 64KB like you had
    byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
    int bytesInBuffer = 0;
    bool isFinal = false;
    bool outerArrayStarted = false;

    var state = new JsonReaderState(new JsonReaderOptions
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    });

    try
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // If the buffer is full but we still haven't consumed it,
            // it means we have a JSON token larger than current capacity.
            // Grow the buffer instead of throwing.
            if (!isFinal && bytesInBuffer == buffer.Length)
            {
                int newSize = buffer.Length * 2;
                byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);

                Buffer.BlockCopy(buffer, 0, newBuffer, 0, bytesInBuffer);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = newBuffer;

                // Optional log:
                // Console.WriteLine($"[JSON] Grew buffer to {newSize:n0} bytes (carried {bytesInBuffer:n0}).");
            }

            int read = 0;

            // Read more only if we haven't reached EOF and there is room
            if (!isFinal && bytesInBuffer < buffer.Length)
            {
                read = await stream.ReadAsync(
                        buffer,
                        bytesInBuffer,
                        buffer.Length - bytesInBuffer,
                        ct)
                    .ConfigureAwait(false);

                if (read == 0)
                    isFinal = true;
            }

            int totalBytes = bytesInBuffer + read;

            // Parse as much as possible
            int bytesConsumed = ParseSpanFromBuffer(
                buffer,
                totalBytes,
                isFinal,
                ref state,
                ref outerArrayStarted,
                onRow);

            int remaining = totalBytes - bytesConsumed;

            if (remaining > 0)
                Buffer.BlockCopy(buffer, bytesConsumed, buffer, 0, remaining);

            bytesInBuffer = remaining;

            // After EOF: keep looping until everything is consumed
            if (isFinal)
            {
                // If we’re final and we can’t make progress, something is wrong
                if (bytesConsumed == 0 && bytesInBuffer > 0)
                    throw new InvalidOperationException(
                        $"EOF reached but {bytesInBuffer} bytes remain unconsumed. JSON truncated or parser stuck.");

                if (bytesInBuffer == 0)
                    break; // fully consumed
            }
        }
    }
    finally
    {
        ArrayPool<byte>.Shared.Return(buffer);
    }
}
