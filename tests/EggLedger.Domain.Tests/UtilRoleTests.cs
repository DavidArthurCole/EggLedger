using EggLedger.Domain.Util;

namespace EggLedger.Domain.Tests;

public class UtilRoleTests {
    [Fact]
    public void RoleFromEB_Farmer() {
        
        var (color, name, addendum, _, _) = Role.RoleFromEB(100.0);
        Assert.Equal("Farmer", name);
        Assert.Equal("d43500", color);
        Assert.Equal("", addendum);
    }

    [Fact]
    public void RoleFromEB_FarmerII() {
        
        var (_, name, addendum, val, _) = Role.RoleFromEB(1000.0);
        Assert.Equal("Farmer II", name);
        Assert.Equal("K", addendum);
        Assert.Equal(1.0, val);
    }

    [Fact]
    public void RoleFromEB_Kilofarmer() {
        
        var (_, name, _, _, _) = Role.RoleFromEB(100000.0);
        Assert.Equal("Kilofarmer", name);
    }
}
