using Ei;

namespace EggLedger.Domain.Ei;

/// <summary>Port of Go ei/backup.go. Preserves exact arithmetic.</summary>
public static class BackupExtensions
{
    /// <summary>
    /// Sum helper mirroring Go's generic Sum. Maps each element to a double and
    /// accumulates. Returns 0 for an empty or null sequence.
    /// </summary>
    public static double Sum<T>(IEnumerable<T>? slice, Func<T, double> toFloat)
    {
        double total = 0;
        if (slice != null)
        {
            foreach (var v in slice)
            {
                total += toFloat(v);
            }
        }
        return total;
    }

    public static Exception? Validate(this EggIncFirstContactResponse fc)
    {
        if (fc.ErrorCode > 0)
        {
            return new InvalidOperationException(
                $"/ei/first_contact: error_code {fc.ErrorCode}");
        }
        if (fc.Backup == null || fc.Backup.game == null)
        {
            return new InvalidOperationException("backup is empty");
        }
        if (fc.Backup.settings == null)
        {
            return new InvalidOperationException("backup settings is empty");
        }
        if (fc.Backup.ArtifactsDb == null)
        {
            return new InvalidOperationException("backup has empty artifacts database");
        }
        return null;
    }

    public static double GetEarningsBonus(this Backup b)
    {
        var virtue = b.virtue;
        var game = b.game;

        double soulEggBonus = 10.0;
        double prophecyEggBonus = 1.05;
        if (game != null)
        {
            foreach (var er in game.EpicResearchs)
            {
                if (er.Id.ToLowerInvariant() == "soul_eggs")
                {
                    soulEggBonus = er.Level + 10;
                }
                else if (er.Id.ToLowerInvariant() == "prophecy_bonus")
                {
                    prophecyEggBonus = (er.Level + 5) / 100.0 + 1;
                }
            }
        }

        double totalPE = game?.EggsOfProphecy ?? 0;
        double peBonus = Math.Pow(prophecyEggBonus, totalPE);

        double totalSE = game?.SoulEggsD ?? 0;
        double seBonus = soulEggBonus * totalSE;

        double totalTEEarned = Sum(virtue?.EovEarneds, v => (double)v);
        double teFactor = Math.Pow(1.01, totalTEEarned);

        double result = peBonus * seBonus * teFactor;
        return result;
    }
}
