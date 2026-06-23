using System.Text.Json;
using EggLedger.Domain.Reports;

namespace EggLedger.Web.Data;

/// <summary>
/// Pure conversions between the persisted <see cref="ReportRow"/> (IndexedDB
/// shape, snake_case wire names) and the <see cref="ReportDefinition"/> that the
/// report engine (<c>RunReportAsync</c>) and the builder use. Port of the
/// row/definition mapping the Go backend does in reportdb/converters; here it is
/// explicit because no mapping existed yet. All methods are pure and tested.
/// </summary>
public static class ReportMapping
{
    private static readonly JsonSerializerOptions FilterOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Builds a <see cref="ReportDefinition"/> from a persisted row, parsing the
    /// filters JSON into the structured <see cref="ReportFilters"/>. Unknown or
    /// blank filters JSON yields empty AND/OR groups.
    /// </summary>
    public static ReportDefinition ToDefinition(ReportRow r) => new()
    {
        Id = r.Id,
        AccountId = r.AccountId,
        Name = r.Name,
        Subject = r.Subject,
        Mode = r.Mode,
        DisplayMode = r.DisplayMode,
        GroupBy = r.GroupBy,
        SecondaryGroupBy = r.SecondaryGroupBy,
        TimeBucket = r.TimeBucket ?? "",
        CustomBucketN = r.CustomBucketN ?? 0,
        CustomBucketUnit = r.CustomBucketUnit ?? "",
        Filters = ParseFilters(r.Filters),
        GridX = r.GridX,
        GridY = r.GridY,
        GridW = r.GridW,
        GridH = r.GridH,
        Weight = r.Weight,
        Color = r.Color,
        Description = r.Description,
        ChartType = r.ChartType,
        SortOrder = r.SortOrder,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        ValueFilterOp = r.ValueFilterOp,
        ValueFilterThreshold = r.ValueFilterThreshold,
        GroupId = r.GroupId,
        NormalizeBy = r.NormalizeBy,
        LabelColors = r.LabelColors,
        UnfilledColor = r.UnfilledColor,
        FamilyWeight = r.FamilyWeight,
        MennoEnabled = r.MennoEnabled,
        MennoCompareMode = r.MennoCompareMode,
        MinSampleSize = r.MinSampleSize,
    };

    /// <summary>
    /// Builds a persisted row from a definition, serializing the structured
    /// filters back to compact JSON. Created/updated timestamps are left for the
    /// store to stamp (it overrides them on insert/update).
    /// </summary>
    public static ReportRow ToRow(ReportDefinition d) => new()
    {
        Id = d.Id,
        AccountId = d.AccountId,
        Name = d.Name,
        Subject = d.Subject,
        Mode = d.Mode,
        DisplayMode = d.DisplayMode,
        GroupBy = d.GroupBy,
        SecondaryGroupBy = d.SecondaryGroupBy,
        TimeBucket = d.TimeBucket,
        CustomBucketN = d.CustomBucketN,
        CustomBucketUnit = d.CustomBucketUnit,
        Filters = SerializeFilters(d.Filters),
        GridX = d.GridX,
        GridY = d.GridY,
        GridW = d.GridW,
        GridH = d.GridH,
        Weight = string.IsNullOrEmpty(d.Weight) ? "LOW" : d.Weight,
        Color = string.IsNullOrEmpty(d.Color) ? "#6366f1" : d.Color,
        Description = d.Description,
        ChartType = string.IsNullOrEmpty(d.ChartType) ? "bar" : d.ChartType,
        SortOrder = d.SortOrder,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        ValueFilterOp = d.ValueFilterOp,
        ValueFilterThreshold = d.ValueFilterThreshold,
        GroupId = d.GroupId,
        NormalizeBy = string.IsNullOrEmpty(d.NormalizeBy) ? "none" : d.NormalizeBy,
        LabelColors = d.LabelColors,
        UnfilledColor = d.UnfilledColor,
        FamilyWeight = d.FamilyWeight,
        MennoEnabled = d.MennoEnabled,
        MennoCompareMode = string.IsNullOrEmpty(d.MennoCompareMode) ? "side_by_side" : d.MennoCompareMode,
        MinSampleSize = d.MinSampleSize,
    };

    /// <summary>Parses filters JSON into structured groups, tolerating blank/invalid input.</summary>
    public static ReportFilters ParseFilters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ReportFilters();
        }
        try
        {
            return JsonSerializer.Deserialize<ReportFilters>(json, FilterOptions) ?? new ReportFilters();
        }
        catch (JsonException)
        {
            return new ReportFilters();
        }
    }

    /// <summary>Serializes structured filters back to compact JSON.</summary>
    public static string SerializeFilters(ReportFilters filters) =>
        JsonSerializer.Serialize(filters, FilterOptions);
}
