namespace EggLedger.Domain.Reports;

/// <summary>
/// Pure rendering math for a Menno community comparison overlaid on a user's 2D
/// heatmap report. Port of the compare-mode computed properties in ReportCard.vue
/// (mennoAirtimeResult / userPerMissionResult / ratioMatrix / ratioResult) plus
/// the gating decision. The actual comparison values come from
/// MennoService.ExecuteComparison; this only reshapes the two ReportResults for
/// the three compare modes so the C3.3 heatmap can render them.
///
/// <para>The comparison renders only for a 2D heatmap report; every other display
/// mode ignores Menno (matching the Vue, where the menno branches are all gated on
/// displayMode === 'heatmap').</para>
/// </summary>
public static class MennoComparison
{
    /// <summary>The three supported compare modes.</summary>
    public const string SideBySide = "side_by_side";
    public const string Ratio = "ratio";
    public const string DualValue = "dual_value";

    /// <summary>
    /// Whether the Menno comparison should render for this report given the user
    /// result and the (possibly null) comparison result. Mirrors the Vue gating:
    /// menno must be enabled, the user result must be a 2D heatmap, and the
    /// comparison result must be present (ExecuteComparison returned non-null).
    /// </summary>
    public static bool ShouldRender(ReportDefinition def, ReportResult? userResult, ReportResult? mennoResult)
    {
        if (def is null || !def.MennoEnabled)
        {
            return false;
        }
        if (def.DisplayMode != "heatmap")
        {
            return false;
        }
        if (userResult is null || !userResult.Is2D || mennoResult is null)
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// The community result reshaped for the side-by-side panel. When normalizeBy
    /// is "airtime" and the comparison carries per-nominal-hour values, those are
    /// substituted so both panels share per-flight-hour units; otherwise the
    /// comparison result is returned unchanged. Port of mennoAirtimeResult.
    /// </summary>
    public static ReportResult MennoAirtimeResult(ReportDefinition def, ReportResult mennoResult)
    {
        if (def.NormalizeBy != "airtime")
        {
            return mennoResult;
        }
        var avm = mennoResult.AirtimeMatrixValues;
        if (avm is null || avm.Count == 0)
        {
            return mennoResult;
        }
        return Clone(mennoResult, matrixValues: new List<double>(avm));
    }

    /// <summary>
    /// The user's result reshaped for dual-value display: per-mission values are
    /// substituted (when present) so the displayed top number divided by the
    /// community bottom number equals the ratio shown by the color. Port of
    /// userPerMissionResult.
    /// </summary>
    public static ReportResult UserPerMissionResult(ReportResult userResult)
    {
        var rpm = userResult.RawPerMissionValues;
        if (rpm is null || rpm.Count == 0)
        {
            return userResult;
        }
        return Clone(userResult, matrixValues: new List<double>(rpm));
    }

    /// <summary>
    /// Per-cell ratio user/menno. Numerator prefers the user's per-mission values
    /// when present. A null entry marks a cell with no community value (menno == 0),
    /// which downstream renders as the unfilled "-" cell. Port of ratioMatrix.
    /// </summary>
    public static IReadOnlyList<double?> RatioMatrix(ReportResult userResult, ReportResult mennoResult)
    {
        var numerator = userResult.RawPerMissionValues is { Count: > 0 } rpm
            ? rpm
            : userResult.MatrixValues;
        var menno = mennoResult.MatrixValues;
        var result = new List<double?>(numerator.Count);
        for (int i = 0; i < numerator.Count; i++)
        {
            double mennoVal = i < menno.Count ? menno[i] : 0;
            if (mennoVal == 0)
            {
                result.Add(null);
                continue;
            }
            result.Add(numerator[i] / mennoVal);
        }
        return result;
    }

    /// <summary>
    /// The ratio result for the "ratio" compare mode: a copy of the user result
    /// with matrix values replaced by the ratio (null -&gt; 0) and IsFloat set.
    /// Port of ratioResult.
    /// </summary>
    public static ReportResult RatioResult(ReportResult userResult, ReportResult mennoResult)
    {
        var ratio = RatioMatrix(userResult, mennoResult);
        var vals = new List<double>(ratio.Count);
        foreach (var v in ratio)
        {
            vals.Add(v ?? 0);
        }
        var clone = Clone(userResult, matrixValues: vals);
        clone.IsFloat = true;
        return clone;
    }

    /// <summary>
    /// Per-cell color values for dual-value mode: the ratio with null entries
    /// flattened to 0 (cellIntensity treats 0 as unfilled). The top/bottom display
    /// numbers come from the per-mission user result and the community result; this
    /// only drives the background intensity. Port of the colorValues prop wiring.
    /// </summary>
    public static IReadOnlyList<double> RatioColorValues(ReportResult userResult, ReportResult mennoResult)
    {
        var ratio = RatioMatrix(userResult, mennoResult);
        var vals = new List<double>(ratio.Count);
        foreach (var v in ratio)
        {
            vals.Add(v ?? 0);
        }
        return vals;
    }

    /// <summary>
    /// Shallow copy of a ReportResult with the matrix values swapped. Reuses the
    /// row/col labels, raw labels, mission counts, weight, and dimensionality of the
    /// source so the heatmap renders identically apart from the supplied values.
    /// </summary>
    private static ReportResult Clone(ReportResult src, List<double> matrixValues) => new()
    {
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
