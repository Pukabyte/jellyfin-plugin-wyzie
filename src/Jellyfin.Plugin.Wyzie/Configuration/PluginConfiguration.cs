using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Wyzie.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Free API key from https://sub.wyzie.io/redeem. Required — the public API
    /// rejects unauthenticated requests with HTTP 401.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public bool IncludeHearingImpaired { get; set; } = true;

    /// <summary>
    /// Wyzie source filter. "all" disables the filter. See
    /// https://sub.wyzie.ru for the current list (subdl, subf2m,
    /// opensubtitles, podnapisi, animetosho, jimaku, kitsunekko, gestdown,
    /// yify, ajatttools).
    /// </summary>
    public string PreferredSource { get; set; } = "all";

    /// <summary>
    /// Preferred subtitle format returned by the API. srt / ass / sub.
    /// </summary>
    public string PreferredFormat { get; set; } = "srt";

    /// <summary>
    /// HTTP retries for transient Wyzie errors (429/5xx).
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
