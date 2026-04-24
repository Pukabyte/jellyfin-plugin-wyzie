using MediaBrowser.Model.Plugins;

namespace Emby.Plugin.Wyzie.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public bool IncludeHearingImpaired { get; set; } = true;
    public string PreferredSource { get; set; } = "all";
    public string PreferredFormat { get; set; } = "srt";
    public int MaxRetries { get; set; } = 3;
}
