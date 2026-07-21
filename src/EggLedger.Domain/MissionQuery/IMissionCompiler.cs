using Ei;

namespace EggLedger.Domain.MissionQuery;

public interface IMissionCompiler {
    IMissionRow CompileMissionInformation(CompleteMissionResponse mission);
}
