namespace EggLedger.Domain.Reports;

public interface IMissionDb {
    IReadOnlyList<object?[]> Query(string sql, IReadOnlyList<object?> args);
}

public interface IWeightData {
    double CraftingWeight(long artifactId, long level);

    IReadOnlyList<int> FamilyAfxIds(string familyId);
}
