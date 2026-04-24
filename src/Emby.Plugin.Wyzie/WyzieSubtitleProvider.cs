using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Wyzie.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using Wyzie.Common;

namespace Emby.Plugin.Wyzie;

public class WyzieSubtitleProvider : ISubtitleProvider
{
    private static readonly HttpClient SharedHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private readonly ILogger _logger;

    public WyzieSubtitleProvider(ILogManager logManager)
    {
        _logger = logManager.GetLogger(nameof(WyzieSubtitleProvider));
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
            _logger.Debug("Wyzie: no TMDB/IMDB id on request for {0}", request.Name ?? "(unknown)");
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

        var client = new WyzieClient(SharedHttp, apiKey: config.ApiKey, maxRetries: config.MaxRetries);
        var subs = await client.SearchAsync(query, cancellationToken).ConfigureAwait(false);

        _logger.Info("Wyzie: {0} subtitle(s) for id={1} s={2} e={3} lang={4}",
            subs.Count, id, query.Season, query.Episode, lang ?? "any");

        return subs.Select(ToRemoteInfo).ToArray();
    }

    public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
    {
        if (!WyzieToken.TryDecode(id, out var url, out var format, out var language))
            throw new ArgumentException($"Unrecognized Wyzie subtitle id: {id}", nameof(id));

        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var client = new WyzieClient(SharedHttp, apiKey: config.ApiKey, maxRetries: config.MaxRetries);
        var stream = await client.OpenSubtitleStreamAsync(url, cancellationToken).ConfigureAwait(false);

        return new SubtitleResponse
        {
            Format = string.IsNullOrWhiteSpace(format) ? "srt" : format,
            Language = language,
            IsForced = false,
            Stream = stream,
        };
    }

    private static RemoteSubtitleInfo ToRemoteInfo(WyzieSubtitle s) => new()
    {
        Id = WyzieToken.Encode(s.Url, s.Format, ThreeLetter(s.Language)),
        ProviderName = "Wyzie",
        Name = FormatName(s),
        Format = s.Format,
        Author = s.Source,
        Comment = s.Release,
        Language = ThreeLetter(s.Language),
    };

    private static string FormatName(WyzieSubtitle s)
    {
        var display = string.IsNullOrWhiteSpace(s.Display) ? (s.FileName ?? s.Url) : s.Display;
        return s.IsHearingImpaired ? display + " (SDH)" : display;
    }

    private static string? ResolveMediaId(SubtitleSearchRequest r)
    {
        if (r.ProviderIds == null) return null;
        if (r.ProviderIds.TryGetValue("Tmdb", out var tmdb) && !string.IsNullOrWhiteSpace(tmdb))
            return tmdb;
        if (r.ProviderIds.TryGetValue("Imdb", out var imdb) && !string.IsNullOrWhiteSpace(imdb))
            return imdb;
        return null;
    }

    private static string? ResolveTwoLetterLanguage(SubtitleSearchRequest r)
    {
        var lang = r.Language;
        if (string.IsNullOrWhiteSpace(lang)) return null;

        // Emby passes 3-letter ISO (e.g. "eng"); Wyzie wants 2-letter.
        if (lang!.Length == 2) return lang.ToLowerInvariant();

        foreach (var ci in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            if (string.Equals(ci.ThreeLetterISOLanguageName, lang, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(ci.TwoLetterISOLanguageName))
            {
                return ci.TwoLetterISOLanguageName.ToLowerInvariant();
            }
        }
        return lang.ToLowerInvariant();
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
