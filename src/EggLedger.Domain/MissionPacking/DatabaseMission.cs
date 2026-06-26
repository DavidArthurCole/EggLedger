using EggLedger.Domain.MissionQuery;
using Ei;

namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// Compiled, display-ready mission record. Go port of missionpacking.DatabaseMission.
/// Ship/DurationType are null when Info was absent (Go pointer fields).
/// </summary>
public sealed class DatabaseMission : IMissionRow {
    /// <summary>Launch Unix timestamp (seconds).</summary>
    public long LaunchDT { get; set; }

    /// <summary>Return Unix timestamp (seconds).</summary>
    public long ReturnDT { get; set; }

    public string MissiondId { get; set; } = "";
    public MissionInfo.Spaceship? Ship { get; set; }
    public string ShipString { get; set; } = "";
    public MissionInfo.DurationType? DurationType { get; set; }
    public string DurationString { get; set; } = "";
    public int Level { get; set; }
    public int Capacity { get; set; }
    public int NominalCapcity { get; set; }
    public bool IsDubCap { get; set; }
    public bool IsBuggedCap { get; set; }
    public string Target { get; set; } = "";
    public int TargetInt { get; set; }
    public int MissionType { get; set; }
    public string MissionTypeString { get; set; } = "";
    public string ShipEnumString { get; set; } = "";
}
