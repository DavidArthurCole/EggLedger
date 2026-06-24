namespace EggLedger.Domain.Reports;

/// <summary>
/// Rendering math for a Menno community comparison on a user's 2D heatmap report.
/// Port of the compare-mode computed properties in ReportCard.vue; renders only for
/// a 2D heatmap (every other display mode ignores Menno).
/// </summary>
public static class MennoComparison {
    /// <summary>The three supported compare modes.</summary>
    public const string SideBySide = "side_by_side";
    public const string Ratio = "ratio";
    public const string DualValue = "dual_value";

    /// <summary>
    /// Whether the comparison should render: menno enabled, user result is a 2D
    /// heatmap, comparison result present. Mirrors the Vue gating.
    /// </summary>
    public static bool ShouldRender(ReportDefinition def, ReportResult? userResult, ReportResult? mennoResult) {
        if (def is null || !def.MennoEnabled) {
            return false;
        }
        if (def.DisplayMode != "heatmap") {
            return false;
        }
        if (userResult is null || !userResult.Is2D || mennoResult is null) {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Community result reshaped for the side-by-side panel. When normalizeBy is
    /// "airtime", per-nominal-hour values are substituted so both panels share units.
    /// Port of mennoAirtimeResult.
    /// </summary>
    public static ReportResult MennoAirtimeResult(ReportDefinition def, ReportResult mennoResult) {
        if (def.NormalizeBy != "airtime") {
            return mennoResult;
        }
        var avm = mennoResult.AirtimeMatrixValues;
        if (avm is null || avm.Count == 0) {
            return mennoResult;
        }
        return Clone(mennoResult, matrixValues: [.. avm]);
    }

    /// <summary>
    /// User's result reshaped for dual-value display: per-mission values substituted
    /// so top/bottom equals the ratio shown by color. Port of userPerMissionResult.
    /// </summary>
    public static ReportResult UserPerMissionResult(ReportResult userResult) {
        var rpm = userResult.RawPerMissionValues;
        if (rpm is null || rpm.Count == 0) {
            return userResult;
        }
        return Clone(userResult, matrixValues: [.. rpm]);
    }

    /// <summary>
    /// Per-cell ratio user/menno; numerator prefers per-mission values. Null marks a
    /// cell with no community value (menno == 0), rendered as the unfilled "-" cell. Port of ratioMatrix.
    /// </summary>
    public static IReadOnlyList<double?> RatioMatrix(ReportResult userResult, ReportResult mennoResult) {
        var numerator = userResult.RawPerMissionValues is { Count: > 0 } rpm
            ? rpm
            : userResult.MatrixValues;
        var menno = mennoResult.MatrixValues;
        var result = new List<double?>(numerator.Count);
        for (int i = 0; i < numerator.Count; i++) {
            double mennoVal = i < menno.Count ? menno[i] : 0;
            if (mennoVal == 0) {
                result.Add(null);
                continue;
            }
            result.Add(numerator[i] / mennoVal);
        }
        return result;
    }

    /// <summary>
    /// Ratio result for "ratio" mode: user result copy with matrix values replaced by
    /// the ratio (null -&gt; 0) and IsFloat set. Port of ratioResult.
    /// </summary>
    public static ReportResult RatioResult(ReportResult userResult, ReportResult mennoResult) {
        var ratio = RatioMatrix(userResult, mennoResult);
        var vals = new List<double>(ratio.Count);
        foreach (var v in ratio) {
            vals.Add(v ?? 0);
        }
        var clone = Clone(userResult, matrixValues: vals);
        clone.IsFloat = true;
        return clone;
    }

    /// <summary>
    /// Per-cell color values for dual-value mode: ratio with nulls flattened to 0
    /// (0 is unfilled). Drives only the background intensity. Port of the colorValues prop wiring.
    /// </summary>
    public static IReadOnlyList<double> RatioColorValues(ReportResult userResult, ReportResult mennoResult) {
        var ratio = RatioMatrix(userResult, mennoResult);
        var vals = new List<double>(ratio.Count);
        foreach (var v in ratio) {
            vals.Add(v ?? 0);
        }
        return vals;
    }

    /// <summary>Shallow copy of a ReportResult with the matrix values swapped; all other fields reused.</summary>
    private static ReportResult Clone(ReportResult src, List<double> matrixValues) => new() {
        Labels = src.Labels,
        Values = src.Values,
        FloatValues = src.FloatValues,
        IsFloat = src.IsFloat,
        Weight = src.Weight,
        RowLabels = src.RowLabels,
        ColLabels = src.ColLabels,
        MatrixValues = matrixValues,
        Is2D = src.Is2D,
        RawRowLabels = src.RawRowLabels,
        RawColLabels = src.RawColLabels,
        RawPerMissionValues = src.RawPerMissionValues,
        AirtimeMatrixValues = src.AirtimeMatrixValues,
        MissionCountMatrix = src.MissionCountMatrix,
    };
}
