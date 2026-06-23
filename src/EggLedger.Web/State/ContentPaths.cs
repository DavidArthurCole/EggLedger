namespace EggLedger.Web.State;

/// <summary>
/// Static-asset paths. Images ship in this Razor Class Library's wwwroot, which
/// the host serves under the RCL content prefix <c>_content/EggLedger.Web/</c>
/// (matching how the CSS and JS are referenced). The Vue app served from the web
/// root used bare <c>images/...</c> paths; the C# port must add the prefix or
/// every icon 404s.
/// </summary>
public static class ContentPaths
{
    /// <summary>RCL static-asset base, e.g. <c>_content/EggLedger.Web/</c>.</summary>
    public const string Base = "_content/EggLedger.Web/";

    /// <summary>Prefixes an RCL-relative asset path (e.g. <c>images/ships/X.png</c>).</summary>
    public static string Asset(string relative) => Base + relative;
}
