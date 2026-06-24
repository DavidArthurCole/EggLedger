using Ei;

namespace EggLedger.Domain.Eiafx;

/// <summary>
/// Supplies the active eiafx <see cref="ArtifactsConfigurationResponse"/>. Default is
/// <see cref="EmbeddedEiafxConfigSource"/>; a host may override to inject a refreshed config.
/// </summary>
public interface IEiafxConfigSource {
    /// <summary>The active artifacts configuration.</summary>
    ArtifactsConfigurationResponse Config { get; }
}
