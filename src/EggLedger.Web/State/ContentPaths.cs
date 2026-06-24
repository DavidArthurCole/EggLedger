namespace EggLedger.Web.State;

/// <summary>Static-asset paths. RCL wwwroot is served under the content prefix <c>_content/EggLedger.Web/</c>; assets must carry it or every icon 404s.</summary>
public static class ContentPaths
{
    /// <summary>RCL static-asset base, e.g. <c>_content/EggLedger.Web/</c>.</summary>
    public const string Base = "_content/EggLedger.Web/";

    /// <summary>Prefixes an RCL-relative asset path (e.g. <c>images/ships/X.png</c>).</summary>
    public static string Asset(string relative) => Base + relative;
}
