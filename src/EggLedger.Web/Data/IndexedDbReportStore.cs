using System.Text.Json;

namespace EggLedger.Web.Data;

/// <summary>
/// IndexedDB-backed reports CRUD (Go reportdb crud + groups). No SQL: ordering and
/// the account/global union are reproduced in memory.
/// </summary>
public sealed class IndexedDbReportStore {
    private const string GlobalAccountId = "__global__";
    private static readonly JsonWriterOptions CompactWriter = new() { Indented = false };
    private readonly IIndexedDb _db;
    private readonly Func<long> _now;

    /// <param name="db">IndexedDB wrapper.</param>
    /// <param name="now">Unix-seconds clock for created_at/updated_at. Defaults to UtcNow; tests inject a fixed value.</param>
    public IndexedDbReportStore(IIndexedDb db, Func<long>? now = null) {
        _db = db;
        _now = now ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>
    /// Empty default when blank, else parsed and re-serialized compactly (key order kept).
    /// Invalid JSON throws, matching Go's marshal error that aborts the write.
    /// </summary>
    public static string NormalizeFiltersJson(string? s) {
        if (string.IsNullOrWhiteSpace(s)) {
            return "{\"and\":[],\"or\":[]}";
        }
        using var doc = JsonDocument.Parse(s);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, CompactWriter)) {
            doc.RootElement.WriteTo(writer);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Coalesces an unset normalize_by to the default 'none'. Mirrors Go normalizeByOrDefault.</summary>
    public static string NormalizeByOrDefault(string? v) =>
        string.IsNullOrEmpty(v) ? Domain.Reports.ReportDefaults.NormalizeNone : v;

    public async Task InsertReportAsync(ReportRow r) {
        long now = _now();
        var row = r with {
            Filters = NormalizeFiltersJson(r.Filters),
            NormalizeBy = NormalizeByOrDefault(r.NormalizeBy),
            CreatedAt = now,
            UpdatedAt = now,
        };
        await _db.PutAsync(IndexedDbStores.Reports, row);
    }

    /// <summary>Updates a report (Go UpdateReport): refreshes updated_at, preserves the stored created_at.</summary>
    public async Task UpdateReportAsync(ReportRow r) {
        long createdAt = r.CreatedAt;
        var existing = await _db.GetAsync<ReportRow>(IndexedDbStores.Reports, r.Id);
        if (existing is not null) {
            createdAt = existing.CreatedAt;
        }
        var row = r with {
            Filters = NormalizeFiltersJson(r.Filters),
            NormalizeBy = NormalizeByOrDefault(r.NormalizeBy),
            CreatedAt = createdAt,
            UpdatedAt = _now(),
        };
        await _db.PutAsync(IndexedDbStores.Reports, row);
    }

    public Task DeleteReportAsync(string id) =>
        _db.DeleteAsync(IndexedDbStores.Reports, id).AsTask();

    public async Task<ReportRow?> RetrieveReportAsync(string id) =>
        await _db.GetAsync<ReportRow>(IndexedDbStores.Reports, id);

    /// <summary>Reports for the account plus globals, ordered by sort_order then created_at. Mirrors Go RetrieveAccountReports.</summary>
    public async Task<IReadOnlyList<ReportRow>> RetrieveAccountReportsAsync(string accountId) {
        var owned = await _db.GetAllByIndexAsync<ReportRow>(IndexedDbStores.Reports, IndexedDbStores.AccountIdIndex, accountId);
        var global = accountId == GlobalAccountId
            ? []
            : await _db.GetAllByIndexAsync<ReportRow>(IndexedDbStores.Reports, IndexedDbStores.AccountIdIndex, GlobalAccountId);
        return owned.Concat(global)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.CreatedAt)
            .ToList();
    }

    /// <summary>Sets each report's sort_order to its index in the list (Go ReorderReports). Missing ids are skipped.</summary>
    public async Task ReorderReportsAsync(IReadOnlyList<string> ids) {
        for (int i = 0; i < ids.Count; i++) {
            var row = await _db.GetAsync<ReportRow>(IndexedDbStores.Reports, ids[i]);
            if (row is null) {
                continue;
            }
            await _db.PutAsync(IndexedDbStores.Reports, row with { SortOrder = i });
        }
    }

    /// <summary>Inserts a group, generating a lowercase-hyphenated UUID when none supplied, stamping created_at. Returns the id.</summary>
    public async Task<string> InsertReportGroupAsync(ReportGroupRow r) {
        string id = string.IsNullOrEmpty(r.Id) ? Guid.NewGuid().ToString("D") : r.Id;
        var row = r with { Id = id, CreatedAt = _now() };
        await _db.PutAsync(IndexedDbStores.ReportGroups, row);
        return id;
    }

    /// <summary>Updates a group's name and sort_order. Mirrors Go UpdateReportGroup.</summary>
    public async Task UpdateReportGroupAsync(ReportGroupRow r) {
        var existing = await _db.GetAsync<ReportGroupRow>(IndexedDbStores.ReportGroups, r.Id);
        // Go UPDATE ... WHERE id=? is a no-op for a missing row; do not insert.
        if (existing is null) {
            return;
        }
        await _db.PutAsync(IndexedDbStores.ReportGroups, existing with { Name = r.Name, SortOrder = r.SortOrder });
    }

    /// <summary>Deletes a group and clears group_id on its member reports. Mirrors Go DeleteReportGroup.</summary>
    public async Task DeleteReportGroupAsync(string id) {
        var members = await RetrieveReportsByGroupAsync(id);
        foreach (var m in members) {
            await _db.PutAsync(IndexedDbStores.Reports, m with { GroupId = "" });
        }
        await _db.DeleteAsync(IndexedDbStores.ReportGroups, id);
    }

    /// <summary>Groups for the account, ordered by sort_order then created_at. Mirrors Go RetrieveAccountGroups (no global union).</summary>
    public async Task<IReadOnlyList<ReportGroupRow>> RetrieveAccountGroupsAsync(string accountId) {
        var rows = await _db.GetAllByIndexAsync<ReportGroupRow>(IndexedDbStores.ReportGroups, IndexedDbStores.AccountIdIndex, accountId);
        return rows.OrderBy(r => r.SortOrder)
            .ThenBy(r => r.CreatedAt)
            .ToList();
    }

    public async Task<ReportGroupRow?> RetrieveReportGroupAsync(string id) =>
        await _db.GetAsync<ReportGroupRow>(IndexedDbStores.ReportGroups, id);

    /// <summary>Reports in a group, ordered by sort_order then created_at. Mirrors Go RetrieveReportsByGroup.</summary>
    public async Task<IReadOnlyList<ReportRow>> RetrieveReportsByGroupAsync(string groupId) {
        var all = await _db.GetAllAsync<ReportRow>(IndexedDbStores.Reports);
        return all.Where(r => r.GroupId == groupId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.CreatedAt)
            .ToList();
    }

    /// <summary>Assigns a report to a group. Mirrors Go SetReportGroup.</summary>
    public async Task SetReportGroupAsync(string reportId, string groupId) {
        var row = await _db.GetAsync<ReportRow>(IndexedDbStores.Reports, reportId);
        if (row is null) {
            return;
        }
        await _db.PutAsync(IndexedDbStores.Reports, row with { GroupId = groupId });
    }
}
