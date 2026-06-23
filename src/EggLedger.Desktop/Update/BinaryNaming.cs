namespace EggLedger.Desktop.Update;

/// <summary>
/// Pure binary-name helpers ported from EggLedger/update/update.go. A freshly
/// downloaded update binary is named "EggLedger_new[.exe]"; after the old instance
/// exits the new instance renames itself to the canonical "EggLedger[.exe]" so the
/// directory keeps a single binary.
/// </summary>
public static class BinaryNaming
{
    /// <summary>Suffix marking a freshly downloaded binary awaiting self-replacement.</summary>
    public const string NewBinarySuffix = "_new";

    private const string ExeExt = ".exe";

    /// <summary>
    /// True when path's base name (minus .exe) ends in "_new", i.e. it is a
    /// downloaded update binary. Ports isNewBinaryName.
    /// </summary>
    public static bool IsNewBinaryName(string path)
    {
        var baseName = Path.GetFileName(path);
        if (baseName.EndsWith(ExeExt, StringComparison.Ordinal))
        {
            baseName = baseName[..^ExeExt.Length];
        }
        return baseName.EndsWith(NewBinarySuffix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Map &lt;dir&gt;/EggLedger_new[.exe] -&gt; &lt;dir&gt;/EggLedger[.exe]. Ports
    /// canonicalPathFromNew.
    /// </summary>
    public static string CanonicalPathFromNew(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var baseName = Path.GetFileName(path);
        var ext = "";
        if (baseName.EndsWith(ExeExt, StringComparison.Ordinal))
        {
            ext = ExeExt;
            baseName = baseName[..^ExeExt.Length];
        }
        if (baseName.EndsWith(NewBinarySuffix, StringComparison.Ordinal))
        {
            baseName = baseName[..^NewBinarySuffix.Length];
        }
        return Path.Combine(dir, baseName + ext);
    }

    /// <summary>
    /// Decide whether this process should run as the updater that replaces
    /// EggLedger[.exe]. Trigger if the running binary is named *_new OR the legacy
    /// --replace-* flags are present. oldPath comes from replacePath when given,
    /// else derived from the _new name. oldPid is 0 when unknown. Ports
    /// decideReplace.
    /// </summary>
    public static (bool Run, int OldPid, string OldPath) DecideReplace(
        string self, int replacePid, string replacePath)
    {
        var hasFlags = replacePid != 0 && !string.IsNullOrEmpty(replacePath);
        var isNew = !string.IsNullOrEmpty(self) && IsNewBinaryName(self);
        if (!isNew && !hasFlags)
        {
            return (false, 0, "");
        }
        var oldPath = replacePath;
        if (string.IsNullOrEmpty(oldPath))
        {
            oldPath = CanonicalPathFromNew(self);
        }
        return (true, replacePid, oldPath);
    }
}
