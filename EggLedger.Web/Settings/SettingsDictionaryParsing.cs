using System.Globalization;

namespace EggLedger.Web.Settings;

internal static class SettingsDictionaryParsing {
    public static bool Bool(IReadOnlyDictionary<string, string> s, string key, bool fallback) =>
        s.TryGetValue(key, out var raw) && bool.TryParse(raw, out var v) ? v : fallback;

    public static int Int(IReadOnlyDictionary<string, string> s, string key, int fallback) =>
        s.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : fallback;

    public static string Str(IReadOnlyDictionary<string, string> s, string key, string fallback) =>
        s.TryGetValue(key, out var raw) ? raw : fallback;
}
