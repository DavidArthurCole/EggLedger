namespace EggLedger.Web.State;

/// <summary>RCL wwwroot serves under <c>_content/EggLedger.Web/</c>; assets must carry that prefix or every icon 404s.</summary>
public static class ContentPaths {
    public const string Base = "_content/EggLedger.Web/";
    public static string Asset(string relative) => Base + relative;
}
