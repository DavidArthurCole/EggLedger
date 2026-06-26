using System.Security.Cryptography;
using System.Text;
using EggLedger.Web.Server.Ships;

namespace EggLedger.Web.Server.Tests.Ships;

public sealed class ShipAssetServiceTests : IDisposable {
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ships-test-" + Guid.NewGuid().ToString("N"));

    public ShipAssetServiceTests() {
        Directory.CreateDirectory(_dir);
        var bytes = Encoding.UTF8.GetBytes("glb-bytes");
        File.WriteAllBytes(Path.Combine(_dir, "ChickenOne.glb"), bytes);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        File.WriteAllText(Path.Combine(_dir, "manifest.json"), $$"""
        {
          "version": "1",
          "generatedFromBuild": null,
          "ships": { "ChickenOne": { "file": "ChickenOne.glb", "sha256": "{{sha}}", "bbox": { "min": [-1,-1,-1], "max": [1,1,1] } } }
        }
        """);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ReadAsync_ReturnsBytesForAvailableShip() {
        var sut = new ShipAssetService(_dir);
        var bytes = await sut.ReadAsync("ChickenOne", CancellationToken.None);
        Assert.NotNull(bytes);
        Assert.Equal("glb-bytes", Encoding.UTF8.GetString(bytes!));
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullForShipNotInManifest() {
        var sut = new ShipAssetService(_dir);
        Assert.Null(await sut.ReadAsync("Henerprise", CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullForUnknownKey() {
        var sut = new ShipAssetService(_dir);
        Assert.Null(await sut.ReadAsync("../secrets", CancellationToken.None));
    }

    [Fact]
    public async Task ReadAsync_ReturnsNullOnShaMismatch() {
        File.WriteAllBytes(Path.Combine(_dir, "ChickenOne.glb"), Encoding.UTF8.GetBytes("tampered"));
        var sut = new ShipAssetService(_dir);
        Assert.Null(await sut.ReadAsync("ChickenOne", CancellationToken.None));
    }

    [Fact]
    public void Manifest_ExposesAvailableKeys() {
        var sut = new ShipAssetService(_dir);
        Assert.Contains("ChickenOne", sut.Manifest.Ships.Keys);
    }
}
