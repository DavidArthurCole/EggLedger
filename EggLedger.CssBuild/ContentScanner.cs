using System.Text.RegularExpressions;

namespace EggLedger.CssBuild;

public static class ContentScanner {
    private static readonly Regex TokenPattern = new(@"!?-?[a-zA-Z0-9_][a-zA-Z0-9_:/.\[\]%!-]*", RegexOptions.Compiled);

    public static HashSet<string> Scan(IEnumerable<string> filePaths) {
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in filePaths) {
            var text = File.ReadAllText(path);
            foreach (Match match in TokenPattern.Matches(text)) {
                var token = match.Value;
                if (token.Length < 2) {
                    continue;
                }
                candidates.Add(token);
            }
        }
        return candidates;
    }
}
