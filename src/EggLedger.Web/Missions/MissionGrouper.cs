using EggLedger.Domain.MissionPacking;

namespace EggLedger.Web.Missions;

/// <summary>A year section with its collapse state.</summary>
public sealed class GroupedYear
{
    public int Year { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>A month section with its collapse state.</summary>
public sealed class GroupedMonth
{
    public int Month { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>A day section with its collapse state.</summary>
public sealed class GroupedDay
{
    public int Day { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>Collapse-state arrays paralleling the mission matrix: year[], month[year][], day[year][month][].</summary>
public sealed class GroupedArrays
{
    public List<GroupedYear> Year { get; set; } = [];
    public List<List<GroupedMonth>> Month { get; set; } = [];
    public List<List<List<GroupedDay>>> Day { get; set; } = [];
}

/// <summary>
/// Result of grouping: the nested mission matrix plus its parallel collapse-state
/// arrays and the "all visible" flag.
/// </summary>
public sealed class MissionGrouping
{
    /// <summary>year -> month -> day -> missions, all date axes descending.</summary>
    public List<List<List<List<DatabaseMission>>>> Missions { get; init; } = [];
    public GroupedArrays Arrays { get; init; } = new();
    public bool AllVisible { get; init; }
}

/// <summary>Groups missions by launch date into year/month/day sections. Single O(N) pass; axes sort descending; per-day lists reversed (matching the Vue .reverse()). When collapseOlderSections is set only the newest year (index 0) is enabled.</summary>
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

        // year -> month -> day -> missions; axes are explicitly sorted descending below.
        var dateMap = new Dictionary<int, Dictionary<int, Dictionary<int, List<DatabaseMission>>>>();
        foreach (var mission in missions)
        {
            var d = ledgerDate(mission.LaunchDT);
            int y = d.Year;
            int mo = d.Month;
            int da = d.Day;
            if (!dateMap.TryGetValue(y, out var ym))
            {
                ym = [];
                dateMap[y] = ym;
            }
            if (!ym.TryGetValue(mo, out var md))
            {
                md = [];
                ym[mo] = md;
            }
            if (!md.TryGetValue(da, out var list))
            {
                list = [];
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
