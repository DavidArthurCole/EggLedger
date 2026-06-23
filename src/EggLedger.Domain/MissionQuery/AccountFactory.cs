using System.Globalization;
using Ei;
using EggLedger.Domain.Ei;
using EggLedger.Domain.Util;

namespace EggLedger.Domain.MissionQuery;

/// <summary>
/// Builds an <see cref="AccountInfo"/> from a first-contact backup. Pure port of
/// the Go <c>addAccount</c> binding's field shaping (main.go): nickname, earnings
/// bonus string + role color, soul-egg abbreviation, prophecy-egg count, and the
/// summed truth-eggs-earned total. Data access (the fetch + persistence) lives in
/// the Web layer; this is the testable arithmetic.
/// </summary>
public static class AccountFactory
{
    /// <summary>
    /// Shapes the display fields for an account from its backup. Mirrors the Go
    /// addAccount binding exactly: EB string formatted to the role precision with
    /// the order-of-magnitude addendum, color from <see cref="Role.RoleFromEB"/>,
    /// SE abbreviated, PE as an integer, TE summed across the Virtue EoV slice.
    /// </summary>
    public static AccountInfo FromBackup(string eid, Backup backup)
    {
        string nickname = backup.UserName;
        double eb = backup.GetEarningsBonus();
        var (roleColor, _, ebAddendum, ebValue, precision) = Role.RoleFromEB(eb);
        string ebString = ebValue.ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + ebAddendum;

        var game = backup.game;
        string seString = Format.AbbreviateFloat(game?.SoulEggsD ?? 0);
        int peCount = (int)(game?.EggsOfProphecy ?? 0);

        int totalTE = 0;
        var virtue = backup.virtue;
        if (virtue?.EovEarneds is { } eov)
        {
            foreach (var v in eov)
            {
                totalTE += (int)v;
            }
        }

        return new AccountInfo
        {
            Id = eid,
            Nickname = nickname,
            EBString = ebString,
            AccountColor = roleColor,
            SeString = seString,
            PeCount = peCount,
            TeCount = totalTE,
        };
    }
}
