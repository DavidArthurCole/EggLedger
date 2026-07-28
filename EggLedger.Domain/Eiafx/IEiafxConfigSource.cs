using Ei;

namespace EggLedger.Domain.Eiafx;

public interface IEiafxConfigSource {
    ArtifactsConfigurationResponse Config { get; }
}
