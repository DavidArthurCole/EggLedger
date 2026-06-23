namespace EggLedger.Domain.MissionPacking;

/// <summary>
/// Precomputed indexed filter-column values. C# port of Go db.MissionFilterCols.
/// </summary>
public struct MissionFilterCols
{
    public int Ship { get; set; }
    public int DurationType { get; set; }
    public int Level { get; set; }
    public int Capacity { get; set; }
    public int NominalCapacity { get; set; }
    public bool IsDubCap { get; set; }
    public bool IsBuggedCap { get; set; }
    public int Target { get; set; }
    public double ReturnTimestamp { get; set; }
}

/// <summary>
/// Lightweight mission record built purely from DB columns. C# port of Go
/// db.MissionMeta.
/// </summary>
public struct MissionMeta
{
    public string MissionId { get; set; }
    public double StartTimestamp { get; set; }
    public double ReturnTimestamp { get; set; }
    public int Ship { get; set; }
    public int DurationType { get; set; }
    public int Level { get; set; }
    public int Capacity { get; set; }
    public int NominalCapacity { get; set; }
    public bool IsDubCap { get; set; }
    public bool IsBuggedCap { get; set; }
    public int Target { get; set; }
    public int MissionType { get; set; }
}
