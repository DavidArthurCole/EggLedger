using Ei;

namespace EggLedger.Domain.Eiafx;

/// <summary>
/// Base-quality lookup. Port of Go eiafx/quality.go, backed by a lazily-built map keyed by
/// (name, level, rarity) so each call is O(1). Matches by value, not pointer identity.
/// </summary>
public static class Quality {
    private readonly record struct SpecKey(
        ArtifactSpec.Name Name,
        ArtifactSpec.Level Level,
        ArtifactSpec.Rarity Rarity);

    private static readonly Lock _gate = new();
    private static Dictionary<SpecKey, double>? _baseQuality;

    /// <summary>
    /// Configured base quality for an artifact spec, matched by
    /// name+level+rarity. Returns 0 if the spec is not in the config.
    /// </summary>
    public static double BaseQualityFor(ArtifactSpec spec) {
        ArgumentNullException.ThrowIfNull(spec);
        var map = Map();
        return map.TryGetValue(new SpecKey(spec.name, spec.level, spec.rarity), out var q) ? q : 0d;
    }

    internal static void ResetCache() {
        lock (_gate) {
            _baseQuality = null;
        }
    }

    private static Dictionary<SpecKey, double> Map() {
        var existing = _baseQuality;
        if (existing != null) {
            return existing;
        }

        lock (_gate) {
            if (_baseQuality != null) {
                return _baseQuality;
            }

            var parameters = EiafxConfig.Config.artifact_parameters;
            var m = new Dictionary<SpecKey, double>(parameters.Count);
            foreach (var art in parameters) {
                var s = art.Spec;
                if (s == null) {
                    continue;
                }
                m[new SpecKey(s.name, s.level, s.rarity)] = art.BaseQuality;
            }
            _baseQuality = m;
            return m;
        }
    }
}
