using Ei;
using ProtoBuf;

namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// Standalone <see cref="IMissionConfigSource"/>: deserializes the embedded
/// eiafx-config.bin (protobuf ArtifactsConfigurationResponse). This is the same
/// data the eiafx package loads, so output matches the Go reference.
///
/// <para>A8 reconciliation: <see cref="EiafxMissionConfigSource"/> is the
/// canonical adapter that binds to the shared <c>EggLedger.Domain.Eiafx.EiafxConfig</c>
/// instead of re-decoding the bin. This embedded source is retained as the
/// self-contained fallback for callers that do not wire the canonical config.</para>
/// </summary>
public sealed class EmbeddedMissionConfigSource : IMissionConfigSource
{
    private const string ResourceName = "EggLedger.Domain.Resources.eiafx-config.bin";

    private static readonly Lazy<ArtifactsConfigurationResponse> _config = new(Load);

    public ArtifactsConfigurationResponse Config => _config.Value;

    private static ArtifactsConfigurationResponse Load()
    {
        var asm = typeof(EmbeddedMissionConfigSource).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"embedded eiafx config resource not found: {ResourceName}");
        return Serializer.Deserialize<ArtifactsConfigurationResponse>(stream);
    }
}
