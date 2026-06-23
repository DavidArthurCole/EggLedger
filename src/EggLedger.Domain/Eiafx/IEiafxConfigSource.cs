using Ei;

namespace EggLedger.Domain.Eiafx;

/// <summary>
/// Supplies the active eiafx <see cref="ArtifactsConfigurationResponse"/>.
/// Domain ships an embedded-.bin default (<see cref="EmbeddedEiafxConfigSource"/>);
/// a host may override to inject a refreshed config. Read-only at runtime, like
/// Go's eiafx.Config.
/// </summary>
public interface IEiafxConfigSource
{
    /// <summary>The active artifacts configuration.</summary>
    ArtifactsConfigurationResponse Config { get; }
}
