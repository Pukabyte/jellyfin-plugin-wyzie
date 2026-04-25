using System;
using System.Collections.Generic;
using System.IO;
using Emby.Plugin.Wyzie.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.Wyzie;

public class Plugin : BasePlugin<PluginConfiguration>, IHasThumbImage, IHasWebPages
{
    public static readonly Guid PluginGuid = Guid.Parse("c3d9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b71");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Wyzie Subtitles";

    public override string Description =>
        "On-demand subtitle provider backed by sub.wyzie.io. Streams subs without caching on disk.";

    public override Guid Id => PluginGuid;

    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name = "WyzieConfigPage",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
            IsMainConfigPage = true,
        },
        new PluginPageInfo
        {
            Name = "WyzieConfigPageJs",
            EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.js",
        },
    };

    public Stream GetThumbImage()
    {
        var type = GetType();
        return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png") ?? Stream.Null;
    }

    public ImageFormat ThumbImageFormat => ImageFormat.Png;
}
