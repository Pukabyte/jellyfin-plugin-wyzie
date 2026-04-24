using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Wyzie.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Wyzie;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginGuid = Guid.Parse("b2c9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b70");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Wyzie Subtitles";

    public override string Description =>
        "On-demand subtitle provider backed by sub.wyzie.ru. Streams subs without caching on disk.";

    public override Guid Id => PluginGuid;

    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
        },
    };
}
