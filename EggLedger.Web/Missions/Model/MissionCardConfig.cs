namespace EggLedger.Web.Missions.Model;

public sealed record MissionCardConfig(
    string Name,
    CardLayout Layout,
    IReadOnlyList<CardFieldSlot> Fields,
    bool ColorizeByDuration,
    bool ShowExpectedDropsPerShip,
    string? BackgroundColor,
    string? BorderColor,
    string? AccentColor,
    string? CustomCss) {
    public static readonly MissionCardConfig Classic = new(
        "Classic",
        CardLayout.CompactRow,
        [
            new CardFieldSlot(CardField.LaunchDate, true, 0),
            new CardFieldSlot(CardField.LaunchTime, true, 1),
            new CardFieldSlot(CardField.ShipName, true, 2),
            new CardFieldSlot(CardField.LevelStars, true, 3),
            new CardFieldSlot(CardField.Target, true, 4),
            new CardFieldSlot(CardField.Duration, false, 5),
            new CardFieldSlot(CardField.Capacity, false, 6),
            new CardFieldSlot(CardField.CapacityModifierBadge, false, 7)
        ],
        true,
        true,
        null,
        null,
        null,
        null);

    public bool Equals(MissionCardConfig? other) =>
        other is not null
        && Name == other.Name
        && Layout == other.Layout
        && ColorizeByDuration == other.ColorizeByDuration
        && ShowExpectedDropsPerShip == other.ShowExpectedDropsPerShip
        && BackgroundColor == other.BackgroundColor
        && BorderColor == other.BorderColor
        && AccentColor == other.AccentColor
        && CustomCss == other.CustomCss
        && Fields.SequenceEqual(other.Fields);

    public override int GetHashCode() => HashCode.Combine(Name, Layout, ColorizeByDuration, Fields.Count);
}

public sealed record CardFieldSlot(CardField Field, bool Enabled, int Order);

public enum CardLayout {
    CompactRow,
    Grid
}

public enum CardField {
    ShipName,
    LevelStars,
    LaunchDate,
    LaunchTime,
    Duration,
    Target,
    Capacity,
    CapacityModifierBadge
}

public sealed record CardPresetSet(IReadOnlyList<MissionCardConfig> Presets, string ActivePresetName) {
    public static readonly CardPresetSet Default = new([MissionCardConfig.Classic], "Classic");

    public bool Equals(CardPresetSet? other) =>
        other is not null && ActivePresetName == other.ActivePresetName && Presets.SequenceEqual(other.Presets);

    public override int GetHashCode() => HashCode.Combine(ActivePresetName, Presets.Count);

    public MissionCardConfig ActivePreset() =>
        Presets.FirstOrDefault(p => p.Name == ActivePresetName) ?? MissionCardConfig.Classic;
}
