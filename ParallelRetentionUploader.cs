using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public static class DatabricksJsonArrayStreamParser
{
    /// <summary>
    /// Parses Databricks SQL EXTERNAL_LINKS payload when format=JSON_ARRAY:
    /// [
    ///   [col1, col2, ...],
    ///   [col1, col2, ...],
    ///   ...
    /// ]
    /// Streams rows without loading entire JSON into memory.
    /// C# 12 safe: Utf8JsonReader only exists inside sync method.
    /// </summary>
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

                // Fill buffer after any carry-over bytes
                int read = await stream.ReadAsync(buffer.AsMemory(bytesInBuffer), ct).ConfigureAwait(false);
                if (read == 0) isFinal = true;

                var span = new ReadOnlySpan<byte>(buffer, 0, bytesInBuffer + read);

                // Sync parse chunk (ref struct is only inside this method)
                int bytesConsumed = ParseSpan(
                    span,
                    isFinal,
                    ref state,
                    ref outerArrayStarted,
                    onRow);

                // Carry over leftover bytes (partial JSON token)
                int remaining = span.Length - bytesConsumed;
                if (remaining > 0)
                {
                    span.Slice(bytesConsumed).CopyTo(buffer);
                }
                bytesInBuffer = remaining;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Synchronous parse step. Safe for C# 12 because Utf8JsonReader ref struct stays here.
    /// Returns number of bytes consumed from the given span.
    /// </summary>
    private static int ParseSpan(
        ReadOnlySpan<byte> span,
        bool isFinal,
        ref JsonReaderState state,
        ref bool outerArrayStarted,
        Action<object?[]> onRow)
    {
        var reader = new Utf8JsonReader(span, isFinal, state);

        while (reader.Read())
        {
            if (!outerArrayStarted)
            {
                if (reader.TokenType == JsonTokenType.StartArray)
                    outerArrayStarted = true;

                continue;
            }

            // Each row is an inner array
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                if (TryReadRow(ref reader, out var row))
                {
                    onRow(row);
                }
                // If false, we ran out of bytes mid-row; state will preserve it.
            }
        }

        state = reader.CurrentState;
        return (int)reader.BytesConsumed;
    }

    /// <summary>
    /// Reads one row array: [v1, v2, ...] and returns it as object?[].
    /// Returns false if the row is incomplete in current buffer.
    /// </summary>
    private static bool TryReadRow(ref Utf8JsonReader reader, out object?[] row)
    {
        var values = new List<object?>(16);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                row = values.ToArray();
                return true;
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    values.Add(null);
                    break;

                case JsonTokenType.String:
                    values.Add(reader.GetString());
                    break;

                case JsonTokenType.True:
                    values.Add(true);
                    break;

                case JsonTokenType.False:
                    values.Add(false);
                    break;

                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long l)) values.Add(l);
                    else if (reader.TryGetDecimal(out decimal d)) values.Add(d);
                    else values.Add(reader.GetDouble());
                    break;

                // Unexpected nested structures for JSON_ARRAY rows
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    row = Array.Empty<object?>();
                    return false;

                default:
                    values.Add(reader.GetString());
                    break;
            }
        }

        // Buffer ended mid-row; we will continue on next chunk
        row = Array.Empty<object?>();
        return false;
    }
}
