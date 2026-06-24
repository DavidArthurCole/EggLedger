using EggLedger.Web.State;

namespace EggLedger.Web.Tests.State;

public sealed class ActiveAccountTests {
    [Fact]
    public void StartsWithNoActiveAccount() {
        var sut = new ActiveAccount();
        Assert.Null(sut.ActiveAccountId);
    }

    [Fact]
    public void SetActiveUpdatesGetterAndFiresChanged() {
        var sut = new ActiveAccount();
        var fired = 0;
        sut.Changed += () => fired++;

        sut.SetActive("EI1234");

        Assert.Equal("EI1234", sut.ActiveAccountId);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SetActiveToSameValueDoesNotFireChanged() {
        var sut = new ActiveAccount();
        sut.SetActive("EI1234");
        var fired = 0;
        sut.Changed += () => fired++;

        sut.SetActive("EI1234");

        Assert.Equal(0, fired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyOrNullIsTreatedAsNoAccount(string? id) {
        var sut = new ActiveAccount();
        sut.SetActive("EI1234");
        var fired = 0;
        sut.Changed += () => fired++;

        sut.SetActive(id);

        Assert.Null(sut.ActiveAccountId);
        Assert.Equal(1, fired);
    }

    [Fact]
    public void ClearingWhenAlreadyEmptyDoesNotFireChanged() {
        var sut = new ActiveAccount();
        var fired = 0;
        sut.Changed += () => fired++;

        sut.SetActive(null);

        Assert.Null(sut.ActiveAccountId);
        Assert.Equal(0, fired);
    }

    [Fact]
    public void SwitchingAccountsFiresChangedEachTime() {
        var sut = new ActiveAccount();
        var fired = 0;
        sut.Changed += () => fired++;

        sut.SetActive("A");
        sut.SetActive("B");

        Assert.Equal("B", sut.ActiveAccountId);
        Assert.Equal(2, fired);
    }
}
