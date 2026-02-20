while (true)
{
    ct.ThrowIfCancellationRequested();

    int read = 0;
    if (!isFinal && bytesInBuffer < buffer.Length)
    {
        read = await stream.ReadAsync(buffer, bytesInBuffer, buffer.Length - bytesInBuffer, ct)
                           .ConfigureAwait(false);
        if (read == 0) isFinal = true;
    }
    else if (!isFinal && bytesInBuffer == buffer.Length)
    {
        throw new InvalidOperationException("Buffer full with unconsumed JSON bytes. Increase buffer or fix parser consumption.");
    }

    int totalBytes = bytesInBuffer + read;

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

    // IMPORTANT: keep looping after EOF until the parser consumes everything
    if (isFinal)
    {
        if (bytesConsumed == 0 && bytesInBuffer > 0)
            throw new InvalidOperationException($"EOF reached but {bytesInBuffer} bytes remain unconsumed. JSON truncated or parser stuck.");

        if (bytesInBuffer == 0)
            break; // fully consumed
    }
}
