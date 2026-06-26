using EggLedger.Domain.Reports;

namespace EggLedger.Web.Data;

/// <summary>
/// Browser report execution over IndexedDB. No SQL engine, so it materializes the
/// player's mission and drop rows and runs them through <see cref="InMemoryReportRunner"/>.
/// </summary>
public sealed class IndexedDbReportRunner : IReportRunner {
    private readonly IIndexedDb _db;
    private readonly IWeightData _weights;

    public IndexedDbReportRunner(IIndexedDb db, IWeightData weights) {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    /// <summary>
    /// Mission and drop rows both come from the <c>player_id</c> index; the SQL impls additionally
    /// scope by current user, the browser impl is single-account, so the result is the player's rows.
    /// </summary>
    public async Task<ReportResult> RunReportAsync(ReportDefinition def, string accountId) {
        var missionRows = await _db.GetAllByIndexAsync<MissionRow>(IndexedDbStores.Mission, IndexedDbStores.PlayerIdIndex, accountId);
        var dropRows = await _db.GetAllByIndexAsync<ArtifactDropRow>(IndexedDbStores.ArtifactDrops, IndexedDbStores.PlayerIdIndex, accountId);

        var missions = missionRows.Select(ToMissionData).ToList();
        var drops = dropRows.Select(ToDropData).ToList();

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
