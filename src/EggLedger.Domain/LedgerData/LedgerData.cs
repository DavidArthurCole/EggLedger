using System.Reflection;
using System.Text.Json;

namespace EggLedger.Domain.LedgerData;

/// <summary>
/// Loads and holds the active ledger display data. C# Domain port of Go
/// ledgerdata: embedded-fallback path only (no file caching, no download).
/// The embedded ledger-display-data-min.json is the source of truth.
/// </summary>
public static class LedgerData
{
    private const string ResourceName = "EggLedger.Domain.Resources.ledger-display-data-min.json";

    private static readonly Lazy<LedgerDisplayData> _config = new(LoadEmbedded);

    /// <summary>The active display data, loaded lazily from the embedded resource.</summary>
    public static LedgerDisplayData Config => _config.Value;

    private static LedgerDisplayData LoadEmbedded()
    {
        var asm = typeof(LedgerData).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"embedded ledger data resource not found: {ResourceName}");

        var data = JsonSerializer.Deserialize<LedgerDisplayData>(stream)
            ?? throw new InvalidOperationException("ledger data deserialized to null");
        return data;
    }

    internal static Assembly OwningAssembly => typeof(LedgerData).Assembly;
}
