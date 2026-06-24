namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Port of Go missionquery.MissionDrop. One artifact/stone/ingredient dropped by a mission.
/// </summary>
public sealed class MissionDrop {
    public int Id { get; set; }
    public string SpecType { get; set; } = "";
    public string Name { get; set; } = "";
    public string GameName { get; set; } = "";
    public string EffectString { get; set; } = "";
    public int Level { get; set; }
    public int Rarity { get; set; }
    public double Quality { get; set; }
    public int IVOrder { get; set; }
}

/// <summary>Port of Go missionquery.PossibleMission. A ship and its per-duration config.</summary>
public sealed class PossibleMission {
    public global::Ei.MissionInfo.Spaceship Ship { get; set; }
    public List<DurationConfig> Durations { get; set; } = [];
}

/// <summary>Port of Go missionquery.DurationConfig.</summary>
public sealed class DurationConfig {
    public global::Ei.MissionInfo.DurationType DurationType { get; set; }
    public double MinQuality { get; set; }
    public double MaxQuality { get; set; }
    public double LevelQualityBump { get; set; }
    public int MaxLevels { get; set; }
}

/// <summary>Port of Go missionquery.DatabaseAccount.</summary>
public sealed class DatabaseAccount {
    public string Id { get; set; } = "";
    public string Nickname { get; set; } = "";
    public int MissionCount { get; set; }
    public string EBString { get; set; } = "";
    public string AccountColor { get; set; } = "";
    public double LastMissionReturnDT { get; set; }
}

/// <summary>
/// Minimal known-account record the store yields for GetExistingData. Subset of
/// Go storage.Account, defined locally to avoid depending on a not-yet-ported storage package.
/// </summary>
public sealed class KnownAccount {
    public string Id { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string EBString { get; set; } = "";
    public string AccountColor { get; set; } = "";
}

/// <summary>
/// Full known-account record. Go port of storage.Account; adds the SE/PE/TE fields
/// the minimal <see cref="KnownAccount"/> omits. Built by <see cref="AccountFactory.FromBackup"/>.
/// </summary>
public sealed class AccountInfo {
    public string Id { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string EBString { get; set; } = "";
    public string AccountColor { get; set; } = "";
    public string SeString { get; set; } = "";
    public int PeCount { get; set; }
    public int TeCount { get; set; }

    /// <summary>Projects the SE/PE/TE-free subset the header consumes.</summary>
    public KnownAccount ToKnownAccount() => new() {
        Id = Id,
        Nickname = Nickname,
        EBString = EBString,
        AccountColor = AccountColor,
    };
}

/// <summary>Per-player mission stats for GetExistingData. Mirrors Go db.RetrievePlayerMissionStats.</summary>
public readonly record struct PlayerMissionStats(int Count, double MaxReturnTimestamp);
