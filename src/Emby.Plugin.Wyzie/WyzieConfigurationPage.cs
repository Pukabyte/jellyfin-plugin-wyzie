using System.IO;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;

namespace Emby.Plugin.Wyzie;

public class WyzieConfigurationPage : IPluginConfigurationPage
{
    public string Name => "Wyzie Subtitles";

    public ConfigurationPageType ConfigurationPageType => ConfigurationPageType.PluginConfiguration;

    public IPlugin Plugin => Emby.Plugin.Wyzie.Plugin.Instance!;

    public Stream GetHtmlStream()
    {
        var type = GetType();
        var stream = type.Assembly.GetManifestResourceStream(type.Namespace + ".Configuration.configPage.html");
        return stream ?? Stream.Null;
    }
}
