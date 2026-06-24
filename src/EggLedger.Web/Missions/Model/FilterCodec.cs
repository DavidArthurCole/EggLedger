using System.Globalization;
using ReportCondition = EggLedger.Domain.Reports.FilterCondition;
using ReportFilters = EggLedger.Domain.Reports.ReportFilters;
using WebCondition = EggLedger.Web.Missions.FilterCondition;

namespace EggLedger.Web.Missions.Model;

// Bridges the persisted/Go-interop string filter shape and the typed MissionFilter; legacy strings live only here and in option-list encodings.
// Operators: = != > < >= <=, d= (same-day), true/false (bool, in Val), c/dnc (drops contains/not). Drops Val glob: name_level_rarity_quality with % wildcards.
public static class FilterCodec {
    public static MissionFilter FromLegacy(ReportFilters legacy) {
        var groups = new List<FilterGroup>();

        // AND list is group 0; each per-index OR-sibling list expands into its own group (legacy "failing-AND rescued by OR sibling" maps to OR-of-groups).
        var andGroup = ToGroup(legacy.And);
        if (andGroup.Conditions.Count > 0) {
            groups.Add(andGroup);
        }

        foreach (var orList in legacy.Or) {
            if (orList is null) {
                continue;
            }
            var g = ToGroup(orList);
            if (g.Conditions.Count > 0) {
                groups.Add(g);
            }
        }

        return new MissionFilter(groups);
    }

    public static FilterGroup FromLegacyAnd(IReadOnlyList<WebCondition> and) {
        var list = new List<Condition>();
        foreach (var c in and) {
            var parsed = FromLegacyCondition(c);
            if (parsed is not null) {
                list.Add(parsed);
            }
        }
        return new FilterGroup(list);
    }

    private static FilterGroup ToGroup(IEnumerable<ReportCondition> conditions) {
        var list = new List<Condition>();
        foreach (var c in conditions) {
            var parsed = ParseCondition(c.TopLevel, c.Op, c.Val);
            if (parsed is not null) {
                list.Add(parsed);
            }
        }
        return new FilterGroup(list);
    }

    public static Condition? FromLegacyCondition(WebCondition c) => ParseCondition(c.TopLevel, c.Op, c.Val);

    // null when the field is unknown or the value does not parse.
    public static Condition? ParseCondition(string topLevel, string op, string val) {
        var c = new WebCondition(topLevel, op, val);
        if (string.IsNullOrEmpty(c.TopLevel)) {
            return null;
        }

        return c.TopLevel switch {
            "buggedcap" => new Condition(FilterField.BuggedCap, BoolOp(c.Val), new FilterValue.Flag(c.Val == "true")),
            "dubcap" => new Condition(FilterField.DubCap, BoolOp(c.Val), new FilterValue.Flag(c.Val == "true")),
            "ship" => EnumCond(FilterField.Ship, c),
            "duration" => EnumCond(FilterField.DurationType, c),
            "farm" or "type" => EnumCond(FilterField.MissionType, c),
            "target" => EnumCond(FilterField.Target, c),
            "level" => NumberCond(FilterField.Level, c),
            "capacity" => NumberCond(FilterField.Capacity, c),
            "launchDT" => DateCond(FilterField.LaunchDate, c),
            "returnDT" => DateCond(FilterField.ReturnDate, c),
            "drops" => DropCond(c),
            _ => null,
        };
    }

    private static Condition? EnumCond(FilterField field, WebCondition c) {
        if (!int.TryParse(c.Val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)) {
            return null;
        }
        return new Condition(field, ParseOp(c.Op), new FilterValue.EnumValue(code));
    }

    private static Condition? NumberCond(FilterField field, WebCondition c) {
        if (!double.TryParse(c.Val, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) {
            return null;
        }
        return new Condition(field, ParseOp(c.Op), new FilterValue.Number(n));
    }

    private static Condition? DateCond(FilterField field, WebCondition c) {
        if (!TryParseDay(c.Val, out var day)) {
            return null;
        }
        return new Condition(field, ParseDateOp(c.Op), new FilterValue.Day(day));
    }

    private static Condition DropCond(WebCondition c) {
        var op = c.Op == "dnc" ? FilterOperator.NotContains : FilterOperator.Contains;
        return new Condition(FilterField.Drops, op, new FilterValue.Drop(DecodeDropGlob(c.Val)));
    }

    private static FilterOperator BoolOp(string val) => val == "false" ? FilterOperator.IsFalse : FilterOperator.IsTrue;

    private static FilterOperator ParseOp(string op) => op switch {
        "=" => FilterOperator.Equals,
        "!=" => FilterOperator.NotEquals,
        ">" => FilterOperator.Greater,
        "<" => FilterOperator.Less,
        ">=" => FilterOperator.GreaterOrEqual,
        "<=" => FilterOperator.LessOrEqual,
        _ => FilterOperator.Equals,
    };

    private static FilterOperator ParseDateOp(string op) => op switch {
        "d=" or "=" => FilterOperator.Equals,
        ">" => FilterOperator.Greater,
        "<" => FilterOperator.Less,
        ">=" => FilterOperator.GreaterOrEqual,
        "<=" => FilterOperator.LessOrEqual,
        _ => FilterOperator.Equals,
    };

    public static DropMatch DecodeDropGlob(string glob) {
        var segs = (glob ?? "").Split('_');
        int? name = Seg(segs, 0);
        int? level = Seg(segs, 1);
        int? rarity = Seg(segs, 2);
        double? quality = SegD(segs, 3);
        return new DropMatch(name, level, rarity, quality);

        static int? Seg(string[] s, int i) {
            if (i >= s.Length || s[i] == "%" || s[i].Length == 0) {
                return null;
            }
            return int.TryParse(s[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        static double? SegD(string[] s, int i) {
            if (i >= s.Length || s[i] == "%" || s[i].Length == 0) {
                return null;
            }
            return double.TryParse(s[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        }
    }

    // Group 0 becomes the AND list; extra groups go to the OR array.
    public static ReportFilters ToLegacy(MissionFilter filter) {
        var result = new ReportFilters();
        for (int i = 0; i < filter.Groups.Count; i++) {
            var legacyGroup = filter.Groups[i].Conditions
                .Select(ToLegacyCondition)
                .Where(x => x is not null)
                .Select(x => new ReportCondition { TopLevel = x!.TopLevel, Op = x.Op, Val = x.Val })
                .ToList();
            if (legacyGroup.Count == 0) {
                continue;
            }
            if (result.And.Count == 0) {
                result.And = legacyGroup;
            } else {
                result.Or.Add(legacyGroup);
            }
        }
        return result;
    }

    public static WebCondition? ToLegacyCondition(Condition c) {
        return c.Field switch {
            FilterField.BuggedCap => new WebCondition("buggedcap", "=", c.Operator == FilterOperator.IsFalse ? "false" : "true"),
            FilterField.DubCap => new WebCondition("dubcap", "=", c.Operator == FilterOperator.IsFalse ? "false" : "true"),
            FilterField.Ship when c.Value is FilterValue.EnumValue e => new WebCondition("ship", OpStr(c.Operator), Int(e.Code)),
            FilterField.DurationType when c.Value is FilterValue.EnumValue e => new WebCondition("duration", OpStr(c.Operator), Int(e.Code)),
            FilterField.MissionType when c.Value is FilterValue.EnumValue e => new WebCondition("type", OpStr(c.Operator), Int(e.Code)),
            FilterField.Target when c.Value is FilterValue.EnumValue e => new WebCondition("target", OpStr(c.Operator), Int(e.Code)),
            FilterField.Level when c.Value is FilterValue.Number n => new WebCondition("level", OpStr(c.Operator), Num(n.N)),
            FilterField.Capacity when c.Value is FilterValue.Number n => new WebCondition("capacity", OpStr(c.Operator), Num(n.N)),
            FilterField.LaunchDate when c.Value is FilterValue.Day d => new WebCondition("launchDT", DateOpStr(c.Operator), DayStr(d.Date)),
            FilterField.ReturnDate when c.Value is FilterValue.Day d => new WebCondition("returnDT", DateOpStr(c.Operator), DayStr(d.Date)),
            FilterField.Drops when c.Value is FilterValue.Drop dr => new WebCondition("drops", c.Operator == FilterOperator.NotContains ? "dnc" : "c", EncodeDropGlob(dr.Match)),
            _ => null,
        };
    }

    public static string EncodeDropGlob(DropMatch m) {
        string name = m.Name?.ToString(CultureInfo.InvariantCulture) ?? "%";
        string level = m.Level?.ToString(CultureInfo.InvariantCulture) ?? "%";
        string rarity = m.Rarity?.ToString(CultureInfo.InvariantCulture) ?? "%";
        string quality = m.Quality?.ToString(CultureInfo.InvariantCulture) ?? "%";
        return $"{name}_{level}_{rarity}_{quality}";
    }

    private static string OpStr(FilterOperator op) => op switch {
        FilterOperator.Equals => "=",
        FilterOperator.NotEquals => "!=",
        FilterOperator.Greater => ">",
        FilterOperator.Less => "<",
        FilterOperator.GreaterOrEqual => ">=",
        FilterOperator.LessOrEqual => "<=",
        _ => "=",
    };

    private static string DateOpStr(FilterOperator op) => op switch {
        FilterOperator.Equals => "d=",
        FilterOperator.Greater => ">",
        FilterOperator.Less => "<",
        FilterOperator.GreaterOrEqual => ">=",
        FilterOperator.LessOrEqual => "<=",
        _ => "d=",
    };

    private static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string Num(double v) => v.ToString(CultureInfo.InvariantCulture);

    private static string DayStr(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool TryParseDay(string s, out DateOnly day) =>
        DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
}
