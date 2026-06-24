using EggLedger.Domain.LedgerData;

namespace EggLedger.Domain.Util;

/// <summary>Farmer role lookup. Go port of util/role.go; reads the loaded ledger config.</summary>
public static class Role
{
    /// <summary>
    /// Resolves the farmer role for an earnings bonus. Returns (color, name, addendum, value, precision)
    /// mirroring the Go RoleFromEB tuple order.
    /// </summary>
    public static (string Color, string Name, string Addendum, double Value, int Precision) RoleFromEB(double earningsBonus)
    {
        var earningsBonusCopy = earningsBonus;
        var ooms = 0;
        while (earningsBonusCopy >= 1e3 && ooms < 17)
        {
            earningsBonusCopy /= 1e3;
            ooms++;
        }
        int precision;
        if (earningsBonusCopy < 10.0)
        {
            precision = 2;
        }
        else if (earningsBonusCopy < 100.0)
        {
            precision = 1;
        }
        else
        {
            precision = 0;
        }

        var roles = LedgerData.LedgerData.Config.FarmerRoles;
        foreach (var role in roles)
        {
            if ((ooms * 3) - precision == role.Oom)
            {
                return (role.Color, role.Name, Format.Addendum(ooms), earningsBonusCopy, precision);
            }
        }
        var last = roles[^1];
        return (last.Color, last.Name, Format.Addendum(last.Oom), earningsBonusCopy, precision);
    }
}
