using Ei;
using ProtoBuf;

namespace EggLedger.Domain.Eiafx;

/// <summary>
/// Default <see cref="IEiafxConfigSource"/>: lazily decodes the embedded eiafx-config.bin once.
/// Wire-equivalent to Go loading eiafx-config-min.json.
/// </summary>
public sealed class EmbeddedEiafxConfigSource : IEiafxConfigSource {
    public static readonly EmbeddedEiafxConfigSource Instance = new();
    private const string ResourceName = "EggLedger.Domain.Resources.eiafx-config.bin";
    private static readonly Lazy<ArtifactsConfigurationResponse> _config = new(LoadEmbedded);

    /// <inheritdoc />
    public ArtifactsConfigurationResponse Config => _config.Value;

    private static ArtifactsConfigurationResponse LoadEmbedded() {
        var asm = typeof(EmbeddedEiafxConfigSource).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"embedded eiafx config resource not found: {ResourceName}");

        return Serializer.Deserialize<ArtifactsConfigurationResponse>(stream);
    }
}
