namespace EggLedger.Web.State;

public static class ContentPaths {
    public const string Base = "_content/EggLedger.Web/";
    public static string Asset(string relative) => Base + relative;
}
