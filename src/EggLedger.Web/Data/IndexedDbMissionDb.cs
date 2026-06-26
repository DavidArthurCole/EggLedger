using EggLedger.Domain.Reports;

namespace EggLedger.Web.Data;

/// <summary>
/// Browser report execution over IndexedDB. No SQL engine, so it materializes the
/// player's mission and drop rows and runs them through <see cref="InMemoryReportRunner"/>.
/// </summary>
public sealed class IndexedDbMissionDb : IReportRunner {
    private const string MissionStore = "mission";
    private const string DropStore = "artifact_drops";
    private const string PlayerIdIndex = "player_id";

    private readonly IIndexedDb _db;
    private readonly IWeightData _weights;

    public IndexedDbMissionDb(IIndexedDb db, IWeightData weights) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    /// <summary>
    /// Mission rows come from the <c>player_id</c> index; drop rows read store-wide
    /// and filtered to the player (browser DB is single-account).
    /// </summary>
    public async Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId) {
        var missionRows = await _db.GetAllByIndexAsync<MissionRow>(MissionStore, PlayerIdIndex, accountId);
        var dropRows = await _db.GetAllAsync<ArtifactDropRow>(DropStore);

        var missions = missionRows
            .Where(m => m.PlayerId == accountId)
            .Select(ToMissionData)
            .ToList();
        var drops = dropRows
            .Where(d => d.PlayerId == accountId)
            .Select(ToDropData)
            .ToList();

        var runner = new InMemoryReportRunner(_weights);
        return runner.Run(def, missions, drops);
    }

    private static MissionRowData ToMissionData(MissionRow r) => new() {
        PlayerId = r.PlayerId,
        MissionId = r.MissionId,
        Ship = r.Ship,
        DurationType = r.DurationType,
        Level = r.Level,
        Target = r.Target,
        MissionType = r.MissionType,
        StartTimestamp = (long)r.StartTimestamp,
        ReturnTimestamp = (long)r.ReturnTimestamp,
        Capacity = r.Capacity,
        NominalCapacity = r.NominalCapacity,
        IsDubCap = r.IsDubCap,
        IsBuggedCap = r.IsBuggedCap,
    };

    private static ArtifactDropRowData ToDropData(ArtifactDropRow r) => new() {
        PlayerId = r.PlayerId,
        MissionId = r.MissionId,
        DropIndex = r.DropIndex,
        ArtifactId = r.ArtifactId,
        SpecType = r.SpecType,
        Level = r.Level,
        Rarity = r.Rarity,
        Quality = r.Quality,
    };
}
