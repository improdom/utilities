using System.Collections.Generic;
using System.Text.Json.Serialization;

internal sealed class ResultChunkResponse
{
    // NOTE: JSON is "external_links" (snake_case)
    [JsonPropertyName("external_links")]
    public ExternalLink[]? ExternalLinks { get; set; }

    [JsonPropertyName("data_array")]
    public object?[][]? DataArray { get; set; }
}

public sealed class ExternalLink
{
    [JsonPropertyName("external_link")]
    public string ExternalLinkUrl { get; set; } = "";

    // NOTE: JSON is an object/dictionary: { "header-name": "value", ... }
    [JsonPropertyName("http_headers")]
    public Dictionary<string, string>? HttpHeaders { get; set; }

    [JsonPropertyName("expiration")]
    public string? Expiration { get; set; }

    // Optional fields you saw in the payload (safe to include)
    [JsonPropertyName("chunk_index")]
    public int? ChunkIndex { get; set; }

    [JsonPropertyName("row_offset")]
    public long? RowOffset { get; set; }

    [JsonPropertyName("row_count")]
    public long? RowCount { get; set; }

    [JsonPropertyName("byte_count")]
    public long? ByteCount { get; set; }

    [JsonPropertyName("next_chunk_index")]
    public int? NextChunkIndex { get; set; }

    [JsonPropertyName("next_chunk_internal_link")]
    public string? NextChunkInternalLink { get; set; }
}
