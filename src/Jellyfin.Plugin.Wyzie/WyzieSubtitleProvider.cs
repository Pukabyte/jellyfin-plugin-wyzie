using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Wyzie.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Wyzie.Common;

namespace Jellyfin.Plugin.Wyzie;

public class WyzieSubtitleProvider : ISubtitleProvider
{
    private readonly ILogger<WyzieSubtitleProvider> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public WyzieSubtitleProvider(ILogger<WyzieSubtitleProvider> logger, IHttpClientFactory httpFactory)
    {
        _logger = logger;
        _httpFactory = httpFactory;
    }

    public string Name => "Wyzie";

    public IEnumerable<VideoContentType> SupportedMediaTypes => new[]
    {
        VideoContentType.Episode,
        VideoContentType.Movie,
    };

    public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var id = ResolveMediaId(request);
        if (id is null)
        {
            _logger.LogDebug("Wyzie: no TMDB/IMDB id on request for {Name}", request.Name);
            return Array.Empty<RemoteSubtitleInfo>();
        }

        var lang = ResolveTwoLetterLanguage(request);
        var query = new WyzieSearchQuery
        {
            Id = id,
            Season = request.ContentType == VideoContentType.Episode ? (int?)request.ParentIndexNumber : null,
            Episode = request.ContentType == VideoContentType.Episode ? (int?)request.IndexNumber : null,
            Language = lang,
            Format = config.PreferredFormat,
            Source = config.PreferredSource,
            HearingImpaired = config.IncludeHearingImpaired ? (bool?)null : false,
        };

        var client = new WyzieClient(_httpFactory.CreateClient(NamedClient), apiKey: config.ApiKey, maxRetries: config.MaxRetries);
        var subs = await client.SearchAsync(query, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Wyzie: {Count} subtitle(s) for id={Id} s={Season} e={Episode} lang={Lang}",
            subs.Count, id, query.Season, query.Episode, lang ?? "any");

        return subs.Select(ToRemoteInfo).ToArray();
    }

    public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
    {
        if (!WyzieToken.TryDecode(id, out var url, out var format, out var language))
            throw new ArgumentException($"Unrecognized Wyzie subtitle id: {id}", nameof(id));

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var client = new WyzieClient(_httpFactory.CreateClient(NamedClient), apiKey: config.ApiKey, maxRetries: config.MaxRetries);
        var stream = await client.OpenSubtitleStreamAsync(url, cancellationToken).ConfigureAwait(false);

        return new SubtitleResponse
        {
            Format = string.IsNullOrWhiteSpace(format) ? "srt" : format,
            Language = language,
            IsForced = false,
            Stream = stream,
        };
    }

    private const string NamedClient = "Wyzie";

    private static RemoteSubtitleInfo ToRemoteInfo(WyzieSubtitle s)
    {
        return new RemoteSubtitleInfo
        {
            Id = WyzieToken.Encode(s.Url, s.Format, ThreeLetter(s.Language)),
            ProviderName = "Wyzie",
            Name = ResolveDisplayName(s),
            Format = s.Format,
            Author = s.Source,
            Comment = s.Origin,
            ThreeLetterISOLanguageName = ThreeLetter(s.Language),
            DownloadCount = s.DownloadCount > int.MaxValue ? int.MaxValue : (int?)s.DownloadCount,
            HearingImpaired = s.IsHearingImpaired,
        };
    }

    private static string ResolveDisplayName(WyzieSubtitle s)
    {
        if (!string.IsNullOrWhiteSpace(s.Release)) return s.Release!;
        if (!string.IsNullOrWhiteSpace(s.FileName)) return s.FileName!;
        if (!string.IsNullOrWhiteSpace(s.Display)) return s.Display;
        return s.Url;
    }

    private string? ResolveMediaId(SubtitleSearchRequest r)
    {
        if (r.ProviderIds == null) return null;
        // Wyzie auto-detects format: tt-prefixed = IMDB, numeric = TMDB.
        // Prefer IMDB because it's universal across providers.
        if (r.ProviderIds.TryGetValue(MetadataProvider.Imdb.ToString(), out var imdb) && !string.IsNullOrWhiteSpace(imdb))
            return imdb;
        if (r.ProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString(), out var tmdb) && !string.IsNullOrWhiteSpace(tmdb))
            return tmdb;
        // Wyzie's API does not accept TVDB IDs; warn so users know why TVDB-only items return nothing.
        if (r.ProviderIds.TryGetValue(MetadataProvider.Tvdb.ToString(), out var tvdb) && !string.IsNullOrWhiteSpace(tvdb))
            _logger.LogDebug("Wyzie: only Tvdb id={Tvdb} available for {Name}; Wyzie requires IMDB or TMDB", tvdb, r.Name);
        return null;
    }

    private static string? ResolveTwoLetterLanguage(SubtitleSearchRequest r)
    {
        if (!string.IsNullOrWhiteSpace(r.TwoLetterISOLanguageName))
            return r.TwoLetterISOLanguageName.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(r.Language))
        {
            try
            {
                var ci = new CultureInfo(r.Language);
                if (!string.IsNullOrEmpty(ci.TwoLetterISOLanguageName))
                    return ci.TwoLetterISOLanguageName.ToLowerInvariant();
            }
            catch (CultureNotFoundException) { }
        }
        return null;
    }

    private static string ThreeLetter(string lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "und";
        try
        {
            var ci = new CultureInfo(lang);
            return string.IsNullOrEmpty(ci.ThreeLetterISOLanguageName) ? lang.ToLowerInvariant() : ci.ThreeLetterISOLanguageName;
        }
        catch (CultureNotFoundException)
        {
            return lang.ToLowerInvariant();
        }
    }
}
