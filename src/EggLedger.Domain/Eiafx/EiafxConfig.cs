using Ei;

namespace EggLedger.Domain.Eiafx;

/// <summary>
/// Static accessor for the active eiafx config, mirroring Go's read-only
/// eiafx.Config global. Defaults to the embedded .bin source. A host may swap
/// the source once at startup via <see cref="SetSource"/>.
/// </summary>
public static class EiafxConfig
{
    private static IEiafxConfigSource _source = EmbeddedEiafxConfigSource.Instance;

    /// <summary>The active artifacts configuration (read-only at runtime).</summary>
    public static ArtifactsConfigurationResponse Config => _source.Config;

    /// <summary>
    /// Overrides the config source. Intended for host wiring at startup, before
    /// first access. Resets the cached base-quality map.
    /// </summary>
    public static void SetSource(IEiafxConfigSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        Quality.ResetCache();
    }
}
