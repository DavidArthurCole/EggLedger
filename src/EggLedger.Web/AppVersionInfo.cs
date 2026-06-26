using System.Reflection;

namespace EggLedger.Web;

// The app release version, set centrally in Directory.Build.props (EggLedgerVersion) and read
// from the entry assembly's InformationalVersion. Both hosts use this; do not hardcode versions.
public static class AppVersionInfo {
    public static string Current {
        get {
            var info = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(info)) {
                return "";
            }
            var plus = info.IndexOf('+', StringComparison.Ordinal);
            return plus >= 0 ? info[..plus] : info;
        }
    }
}
