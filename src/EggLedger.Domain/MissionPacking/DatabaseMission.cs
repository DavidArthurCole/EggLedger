using EggLedger.Domain.MissionQuery;
using Ei;

namespace EggLedger.Domain.MissionPacking;

public sealed class DatabaseMission : IMissionRow {
    public long LaunchDT { get; set; }

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
