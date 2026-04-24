using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Wyzie.Common;

/// <summary>
/// Thin wrapper around https://sub.wyzie.ru/ with exponential-backoff retry
/// for transient failures. Stateless — no disk or in-process caching.
/// </summary>
public sealed class WyzieClient
{
    public const string DefaultBaseUrl = "https://sub.wyzie.io";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string? _apiKey;
    private readonly int _maxRetries;

    public WyzieClient(HttpClient http, string? apiKey = null, string? baseUrl = null, int maxRetries = 3)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _maxRetries = Math.Max(0, maxRetries);
    }

    public async Task<IReadOnlyList<WyzieSubtitle>> SearchAsync(WyzieSearchQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Id))
            return Array.Empty<WyzieSubtitle>();

        var url = BuildSearchUrl(query);
        var body = await SendWithRetryAsync(url, ct).ConfigureAwait(false);
        if (body is null) return Array.Empty<WyzieSubtitle>();

        try
        {
            var list = JsonSerializer.Deserialize<List<WyzieSubtitle>>(body, JsonOpts);
            return list ?? (IReadOnlyList<WyzieSubtitle>)Array.Empty<WyzieSubtitle>();
        }
        catch (JsonException)
        {
            return Array.Empty<WyzieSubtitle>();
        }
    }

    /// <summary>
    /// Open a streaming response for a subtitle file URL. Caller owns the
    /// returned stream and is responsible for disposing it.
    /// </summary>
    public async Task<Stream> OpenSubtitleStreamAsync(string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("url is required", nameof(url));

        var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
#if NET5_0_OR_GREATER
        return await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
#else
        return await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
    }

    private string BuildSearchUrl(WyzieSearchQuery q)
    {
        var sb = new StringBuilder(_baseUrl).Append("/search?id=").Append(Uri.EscapeDataString(q.Id));
        if (q.Season.HasValue) sb.Append("&season=").Append(q.Season.Value);
        if (q.Episode.HasValue) sb.Append("&episode=").Append(q.Episode.Value);
        if (!string.IsNullOrWhiteSpace(q.Language)) sb.Append("&language=").Append(Uri.EscapeDataString(q.Language!));
        if (!string.IsNullOrWhiteSpace(q.Format)) sb.Append("&format=").Append(Uri.EscapeDataString(q.Format!));
        if (!string.IsNullOrWhiteSpace(q.Source) && !string.Equals(q.Source, "all", StringComparison.OrdinalIgnoreCase))
            sb.Append("&source=").Append(Uri.EscapeDataString(q.Source!));
        if (q.HearingImpaired.HasValue) sb.Append("&hi=").Append(q.HearingImpaired.Value ? "true" : "false");
        if (_apiKey != null) sb.Append("&key=").Append(Uri.EscapeDataString(_apiKey));
        return sb.ToString();
    }

    private async Task<string?> SendWithRetryAsync(string url, CancellationToken ct)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
                if (resp.StatusCode == HttpStatusCode.OK)
                    return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (IsTransient(resp.StatusCode) && attempt < _maxRetries)
                {
                    await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
                    continue;
                }
                return null;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = null;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
                    continue;
                }
                return null;
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
                if (attempt < _maxRetries)
                {
                    await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
                    continue;
                }
                return null;
            }
        }
        _ = lastError;
        return null;
    }

    private static bool IsTransient(HttpStatusCode code) =>
        code == HttpStatusCode.RequestTimeout
        || (int)code == 429
        || code == HttpStatusCode.BadGateway
        || code == HttpStatusCode.ServiceUnavailable
        || code == HttpStatusCode.GatewayTimeout;

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds(1000 * Math.Pow(2, attempt) + attempt * 100);
}
