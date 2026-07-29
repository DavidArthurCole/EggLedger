using EggLedger.Domain.LedgerData;

namespace EggLedger.Domain.Util;

public static class Role {
    public static (string Color, string Name, string Addendum, double Value, int Precision) RoleFromEB(double earningsBonus) {
        var earningsBonusCopy = earningsBonus;
        var ooms = 0;
        while (earningsBonusCopy >= 1e3 && ooms < 17) {
            earningsBonusCopy /= 1e3;
            ooms++;
        }
        int precision;
        if (earningsBonusCopy < 10.0) {
            precision = 2;
        } else if (earningsBonusCopy < 100.0) {
            precision = 1;
        } else {
            precision = 0;
        }

        var roles = LedgerData.LedgerData.Config.FarmerRoles;
        foreach (var role in roles) {
            if ((ooms * 3) - precision == role.Oom) {
                return (role.Color, role.Name, Format.Addendum(ooms), earningsBonusCopy, precision);
            }
        }
        var last = roles[^1];
        return (last.Color, last.Name, Format.Addendum(last.Oom), earningsBonusCopy, precision);
    }
}
