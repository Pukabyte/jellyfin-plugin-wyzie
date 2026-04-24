using System.Text.Json.Serialization;

namespace Wyzie.Common;

public sealed class WyzieSubtitle
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("flagUrl")]
    public string? FlagUrl { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; } = "srt";

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; } = "utf-8";

    [JsonPropertyName("media_type")]
    public string? MediaType { get; set; }

    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("isHearingImpaired")]
    public bool IsHearingImpaired { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("release")]
    public string? Release { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
}
