using System.Globalization;
using Ei;
using EggLedger.Domain.LedgerData;

namespace EggLedger.Domain.Ei;

/// <summary>Port of Go ei/missions.go.</summary>
public static class MissionExtensions
{
    private static LedgerDisplayData Config => LedgerData.LedgerData.Config;

    public static string Name(this MissionInfo.Spaceship s) =>
        Config.ShipNames.TryGetValue(EnumNames.ProtoName(s), out var name)
            ? name
            : EnumNames.ProtoName(s);

    public static string GetDurationString(this MissionInfo d)
    {
        double seconds = d.DurationSeconds;
        if (seconds == 0)
        {
            return "0m";
        }
        if (seconds < 60)
        {
            return $"{(int)seconds}s";
        }
        if (seconds < 3600)
        {
            return $"{(int)(seconds / 60)}m";
        }
        if (seconds < 86400)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}h{1}m",
                (int)(seconds / 3600),
                (int)(seconds / 60) % 60);
        }
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}d{1}h{2}m",
            (int)(seconds / 86400),
            (int)(seconds / 3600) % 24,
            (int)(seconds / 60) % 60);
    }

    public static string Display(this MissionInfo.DurationType d)
    {
        switch (d)
        {
            case MissionInfo.DurationType.Tutorial:
                return "Tutorial";
            case MissionInfo.DurationType.Short:
                return "Short";
            case MissionInfo.DurationType.Long:
                return "Standard";
            case MissionInfo.DurationType.Epic:
                return "Extended";
        }
        return "Unknown";
    }

    public static string Display(this MissionInfo.MissionType t)
    {
        switch (t)
        {
            case MissionInfo.MissionType.Standard:
                return "Home";
            case MissionInfo.MissionType.Virtue:
                return "Virtue";
        }
        return "Unknown";
    }

    public static List<MissionInfo> GetCompletedMissions(this EggIncFirstContactResponse fc)
    {
        var afxdb = fc.Backup?.ArtifactsDb;
        var allMissions = new List<MissionInfo>();
        if (afxdb != null)
        {
            allMissions.AddRange(afxdb.MissionArchives);
            allMissions.AddRange(afxdb.MissionInfos);
        }

        var completed = new List<MissionInfo>();
        // Dedupe: the archive can contain duplicates even without intentional glitching.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mission in allMissions)
        {
            var status = mission.status;
            if (status == MissionInfo.Status.Complete || status == MissionInfo.Status.Archived)
            {
                var id = mission.Identifier;
                if (seen.Add(id))
                {
                    completed.Add(mission);
                }
            }
        }

        return StableSortByStartTime(completed);
    }

    public static List<MissionInfo> GetInProgressMissions(this EggIncFirstContactResponse fc)
    {
        var inProgress = new List<MissionInfo>();
        var afxdb = fc.Backup?.ArtifactsDb;
        if (afxdb != null)
        {
            foreach (var mission in afxdb.MissionInfos)
            {
                var status = mission.status;
                if (status == MissionInfo.Status.Exploring
                    || status == MissionInfo.Status.Fueling
                    || status == MissionInfo.Status.PrepareToLaunch)
                {
                    inProgress.Add(mission);
                }
            }
        }
        return StableSortByStartTime(inProgress);
    }

    private static List<MissionInfo> StableSortByStartTime(List<MissionInfo> missions) =>
        missions
            .Select((m, i) => (m, i))
            .OrderBy(x => x.m.StartTimeDerived)
            .ThenBy(x => x.i)
            .Select(x => x.m)
            .ToList();
}
