using EggLedger.Domain.Util;

namespace EggLedger.Domain.Tests;

public class UtilRoleTests
{
    [Fact]
    public void RoleFromEB_Farmer()
    {
        // EB=100: ooms=0, value=100.0, precision=0, OOM index=0 -> "Farmer"
        var (color, name, addendum, _, _) = Role.RoleFromEB(100.0);
        Assert.Equal("Farmer", name);
        Assert.Equal("d43500", color);
        Assert.Equal("", addendum);
    }

    [Fact]
    public void RoleFromEB_FarmerII()
    {
        // EB=1000: ooms=1, value=1.0, precision=2, OOM index=1 -> "Farmer II"
        var (_, name, addendum, val, _) = Role.RoleFromEB(1000.0);
        Assert.Equal("Farmer II", name);
        Assert.Equal("K", addendum);
        Assert.Equal(1.0, val);
    }

    [Fact]
    public void RoleFromEB_Kilofarmer()
    {
        // EB=100000: ooms=1, value=100.0, precision=0, OOM index=3 -> "Kilofarmer"
        var (_, name, _, _, _) = Role.RoleFromEB(100000.0);
        Assert.Equal("Kilofarmer", name);
    }
}
