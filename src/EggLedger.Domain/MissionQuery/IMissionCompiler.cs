using Ei;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Compiles a decoded complete mission into a display row (slow path).
/// Split off IMissionStore: a pure compile step, not data access. Mirrors
/// missionpacking.CompileMissionInformation; injected to avoid a hard dependency.
/// </summary>
public interface IMissionCompiler {
    IMissionRow CompileMissionInformation(CompleteMissionResponse mission);
}
