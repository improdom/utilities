
var http = DatabricksExternalLinkJsonArrayLoader.CreateRobustHttpClient(TimeSpan.FromMinutes(10));

long emitted = await DatabricksExternalLinkJsonArrayLoader.DownloadAndStreamRowsWithRetryAsync(
    http,
    link.ExternalLinkUrl,
    link.HttpHeaders,      // IDictionary<string,string> if you have it
    link.RowCount,         // expected
    row => AppendRowToCache(row, ord, rows, byMeasure, byScenario, byMdst),
    ct);



using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public static class DatabricksExternalLinkJsonArrayLoader
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        // External links payload is pure JSON; keep strict.
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    public static async Task<long> DownloadAndStreamRowsWithRetryAsync(
        HttpClient http,
        string externalLinkUrl,
        IDictionary<string, string>? headers,
        long expectedRowCount,
        Action<object?[]> onRow,
        CancellationToken ct,
        int maxAttempts = 3)
    {
        Exception? last = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                long emitted = await DownloadAndStreamRowsOnceAsync(
                    http, externalLinkUrl, headers, onRow, ct);

                // Validate deterministically
                if (expectedRowCount >= 0 && emitted != expectedRowCount)
                {
                    throw new InvalidOperationException(
                        $"RowCount mismatch. Expected={expectedRowCount}, Emitted={emitted}, Url={externalLinkUrl}");
                }

                return emitted;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRetryable(ex))
            {
                last = ex;

                // Backoff with jitter (keeps pressure off Databricks / proxy)
                var delayMs = (int)(200 * Math.Pow(2, attempt - 1)) + Random.Shared.Next(0, 200);
                await Task.Delay(delayMs, ct);
            }
        }

        throw new InvalidOperationException(
            $"Failed after {maxAttempts} attempts. Url={externalLinkUrl}", last);
    }

    private static async Task<long> DownloadAndStreamRowsOnceAsync(
        HttpClient http,
        string externalLinkUrl,
        IDictionary<string, string>? headers,
        Action<object?[]> onRow,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, externalLinkUrl);

        // Databricks may require headers for signed URLs
        if (headers != null)
        {
            foreach (var kv in headers)
            {
                // Avoid header validation issues (signed URLs sometimes include unusual headers)
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
        }

        // Ensure we ask for compressed content (handler will decompress if configured)
        req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

        using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);

        // Stream each row without loading entire JSON into memory
        long emitted = 0;

        // We deserialize the OUTER array as JsonElement items. Each item should be a row array.
        await foreach (JsonElement rowElem in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(stream, JsonOpts, ct))
        {
            ct.ThrowIfCancellationRequested();

            if (rowElem.ValueKind == JsonValueKind.Undefined || rowElem.ValueKind == JsonValueKind.Null)
                continue;

            if (rowElem.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"Expected row to be JSON array but got {rowElem.ValueKind}.");

            // Convert row (JsonElement array) into object?[] (supports primitives + nested values)
            object?[] row = ConvertRow(rowElem);
            onRow(row);
            emitted++;
        }

        return emitted;
    }

    private static object?[] ConvertRow(JsonElement rowElem)
    {
        int len = rowElem.GetArrayLength();
        var row = new object?[len];

        int i = 0;
        foreach (var cell in rowElem.EnumerateArray())
        {
            row[i++] = ConvertCell(cell);
        }

        return row;
    }

    private static object? ConvertCell(JsonElement cell)
    {
        // Convert primitives to CLR types; keep complex types as JsonElement (safe, lossless)
        return cell.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => cell.GetString(),

            JsonValueKind.Number => cell.TryGetInt64(out var l) ? l
                                 : cell.TryGetDouble(out var d) ? d
                                 : cell.GetDecimal(),

            // If Databricks returns nested arrays/objects in a column,
            // keep it as JsonElement (caller can decide what to do).
            JsonValueKind.Object => cell.Clone(),
            JsonValueKind.Array => cell.Clone(),

            _ => cell.Clone()
        };
    }

    private static bool IsRetryable(Exception ex)
    {
        // JSON parse errors can come from truncation; retry is reasonable.
        // Network glitches are retryable.
        if (ex is HttpRequestException) return true;
        if (ex is IOException) return true;
        if (ex is TaskCanceledException) return true; // includes timeouts
        if (ex is JsonException) return true;

        // RowCount mismatch can happen if the stream was truncated but still valid JSON (rare).
        // Retrying often fixes it.
        if (ex is InvalidOperationException ioe && ioe.Message.Contains("RowCount mismatch", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Create an HttpClient that auto-decompresses (important).
    /// </summary>
    public static HttpClient CreateRobustHttpClient(TimeSpan timeout)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
        };

        return new HttpClient(handler)
        {
            Timeout = timeout
        };
    }
}

