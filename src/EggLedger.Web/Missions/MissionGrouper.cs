using EggLedger.Domain.MissionPacking;

namespace EggLedger.Web.Missions;

/// <summary>A year section with its collapse state. Port of Vue GroupedYear.</summary>
public sealed class GroupedYear
{
    public int Year { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>A month section with its collapse state. Port of Vue GroupedMonth.</summary>
public sealed class GroupedMonth
{
    public int Month { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>A day section with its collapse state. Port of Vue GroupedDay.</summary>
public sealed class GroupedDay
{
    public int Day { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>
/// The collapse-state arrays paralleling the mission matrix. Port of Vue
/// GroupedArrays: year[], month[year][], day[year][month][].
/// </summary>
public sealed class GroupedArrays
{
    public List<GroupedYear> Year { get; set; } = new();
    public List<List<GroupedMonth>> Month { get; set; } = new();
    public List<List<List<GroupedDay>>> Day { get; set; } = new();
}

/// <summary>
/// Result of grouping: the nested mission matrix plus its parallel collapse-state
/// arrays and the "all visible" flag.
/// </summary>
public sealed class MissionGrouping
{
    /// <summary>year -> month -> day -> missions, all date axes descending.</summary>
    public List<List<List<List<DatabaseMission>>>> Missions { get; init; } = new();
    public GroupedArrays Arrays { get; init; } = new();
    public bool AllVisible { get; init; }
}

/// <summary>
/// Groups missions by launch date into year/month/day sections. C# port of the
/// pure grouping core of www/src/composables/useMissionListGrouping.ts. Single
/// O(N) pass builds a year-&gt;month-&gt;day map; axes sort descending; per-day
/// mission lists are reversed (matching the Vue <c>.reverse()</c>). Collapse
/// state: when <paramref name="collapseOlderSections"/> is set only the newest
/// year (index 0) is enabled.
/// </summary>
public static class MissionGrouper
{
    public static MissionGrouping Group(
        IReadOnlyList<DatabaseMission>? missions,
        Func<long, DateTime> ledgerDate,
        bool collapseOlderSections)
    {
        if (missions is null || missions.Count == 0)
        {
            return new MissionGrouping { AllVisible = true };
        }

        // year -> month -> day -> missions, insertion order tracked for nothing
        // (axes are explicitly sorted descending below).
        var dateMap = new Dictionary<int, Dictionary<int, Dictionary<int, List<DatabaseMission>>>>();
        foreach (var mission in missions)
        {
            var d = ledgerDate(mission.LaunchDT);
            int y = d.Year;
            int mo = d.Month;
            int da = d.Day;
            if (!dateMap.TryGetValue(y, out var ym))
            {
                ym = new Dictionary<int, Dictionary<int, List<DatabaseMission>>>();
                dateMap[y] = ym;
            }
            if (!ym.TryGetValue(mo, out var md))
            {
                md = new Dictionary<int, List<DatabaseMission>>();
                ym[mo] = md;
            }
            if (!md.TryGetValue(da, out var list))
            {
                list = new List<DatabaseMission>();
                md[da] = list;
            }
            list.Add(mission);
        }

        var uniqueYears = new List<int>(dateMap.Keys);
        uniqueYears.Sort((a, b) => b - a);

        var arrays = new GroupedArrays();
        var matrix = new List<List<List<List<DatabaseMission>>>>();

        for (int yi = 0; yi < uniqueYears.Count; yi++)
        {
            int y = uniqueYears[yi];
            bool yearEnabled = !collapseOlderSections || yi == 0;
            arrays.Year.Add(new GroupedYear { Year = y, Enabled = yearEnabled });

            var months = new List<int>(dateMap[y].Keys);
            months.Sort((a, b) => b - a);

            var monthStates = new List<GroupedMonth>();
            var dayStatesForYear = new List<List<GroupedDay>>();
            var yearMatrix = new List<List<List<DatabaseMission>>>();

            foreach (int mo in months)
            {
                monthStates.Add(new GroupedMonth { Month = mo, Enabled = yearEnabled });

                var days = new List<int>(dateMap[y][mo].Keys);
                days.Sort((a, b) => b - a);

                var dayStates = new List<GroupedDay>();
                var monthMatrix = new List<List<DatabaseMission>>();
                foreach (int da in days)
                {
                    dayStates.Add(new GroupedDay { Day = da, Enabled = yearEnabled });
                    var dayMissions = new List<DatabaseMission>(dateMap[y][mo][da]);
                    dayMissions.Reverse();
                    monthMatrix.Add(dayMissions);
                }
                dayStatesForYear.Add(dayStates);
                yearMatrix.Add(monthMatrix);
            }

            arrays.Month.Add(monthStates);
            arrays.Day.Add(dayStatesForYear);
            matrix.Add(yearMatrix);
        }

        bool allVisible = !collapseOlderSections || uniqueYears.Count <= 1;

        return new MissionGrouping
        {
            Missions = matrix,
            Arrays = arrays,
            AllVisible = allVisible,
        };
    }
}
