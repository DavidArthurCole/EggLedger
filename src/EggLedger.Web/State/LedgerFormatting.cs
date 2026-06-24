using System.Globalization;
using EggLedger.Domain.MissionQuery;

namespace EggLedger.Web.State;

/// <summary>Pure presentation helpers for the Ledger tab, extracted so the .razor stays thin and the logic is unit-testable.</summary>
public static class LedgerFormatting {
    /// <summary>Accounts ordered by stored mission count, descending. Stable for equal counts (preserves input order).</summary>
    public static IReadOnlyList<DatabaseAccount> SortByMissionCountDescending(IEnumerable<DatabaseAccount> accounts) =>
        accounts
            .Select((acct, index) => (acct, index))
            .OrderByDescending(x => x.acct.MissionCount)
            .ThenBy(x => x.index)
            .Select(x => x.acct)
            .ToList();

    /// <summary>Relative "X ago" label from a unix-seconds timestamp; empty for missing/future, else the two coarsest non-zero units.</summary>
    public static string FormatTimeSince(double returnUnixSeconds, double nowUnixSeconds) {
        if (returnUnixSeconds == 0) {
            return "";
        }
        double diff = nowUnixSeconds - returnUnixSeconds;
        if (diff <= 0) {
            return "";
        }
        long totalMinutes = (long)Math.Floor(diff / 60);
        long days = totalMinutes / (60 * 24);
        long hours = totalMinutes % (60 * 24) / 60;
        long minutes = totalMinutes % 60;
        if (days > 0) {
            return hours > 0 ? $"{days}d{hours}h ago" : $"{days}d ago";
        }
        if (hours > 0) {
            return minutes > 0 ? $"{hours}h{minutes}m ago" : $"{hours}h ago";
        }
        return $"{minutes}m ago";
    }

    /// <summary>Filters accounts by case-insensitive substring against id or nickname; a blank query returns input unchanged.</summary>
    public static IReadOnlyList<DatabaseAccount> FilterAccounts(IReadOnlyList<DatabaseAccount> accounts, string? query) {
        if (string.IsNullOrEmpty(query)) {
            return accounts;
        }
        string q = query.ToLower(CultureInfo.InvariantCulture);
        return accounts
            .Where(a =>
                a.Id.ToLower(CultureInfo.InvariantCulture).Contains(q, StringComparison.Ordinal)
                || a.Nickname.ToLower(CultureInfo.InvariantCulture).Contains(q, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>Trim + uppercase an EID.</summary>
    public static string NormalizeEid(string? eid) =>
        (eid ?? "").Trim().ToUpper(CultureInfo.InvariantCulture);

    /// <summary>Validation message for a normalized candidate EID, or empty when valid (empty input yields empty, no error).</summary>
    public static string EidProblem(string normalizedEid) {
        string v = normalizedEid;
        if (v == "") {
            return "";
        }
        if (!v.StartsWith("EI", StringComparison.Ordinal)) {
            return "Player ID must start with \"EI\"";
        }
        if (v.Length < 18) {
            return "Player ID is too short (expected EI + 16 digits)";
        }
        if (v.Length > 18) {
            return "Player ID is too long (expected EI + 16 digits)";
        }
        if (!IsEiPlusSixteenDigits(v)) {
            return "Player ID must be EI followed by exactly 16 digits";
        }
        return "";
    }

    /// <summary>True when the normalized EID is non-empty and has no problem.</summary>
    public static bool IsEidValid(string normalizedEid) =>
        normalizedEid != "" && EidProblem(normalizedEid) == "";

    private static bool IsEiPlusSixteenDigits(string v) {
        if (v.Length != 18 || v[0] != 'E' || v[1] != 'I') {
            return false;
        }
        for (int i = 2; i < 18; i++) {
            if (!char.IsAsciiDigit(v[i])) {
                return false;
            }
        }
        return true;
    }
}
