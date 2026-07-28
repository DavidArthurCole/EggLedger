using System.Security.Cryptography;
using System.Text;
using EggLedger.Web.Server.Ships;

namespace EggLedger.Web.Server.Tests.Ships;

public sealed class ShipEndpointsTests : IDisposable {
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ships-ep-" + Guid.NewGuid().ToString("N"));

    public ShipEndpointsTests() {
        Directory.CreateDirectory(_dir);
        var bytes = Encoding.UTF8.GetBytes("glb-bytes");
        File.WriteAllBytes(Path.Combine(_dir, "ChickenOne.glb"), bytes);
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        File.WriteAllText(Path.Combine(_dir, "manifest.json"), $$"""
        { "version": "1", "generatedFromBuild": null,
          "ships": { "ChickenOne": { "file": "ChickenOne.glb", "sha256": "{{sha}}", "bbox": { "min": [-1,-1,-1], "max": [1,1,1] } } } }
        """);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ShipAssetService Service() => new(_dir);

    [Fact]
    public async Task Glb_NonAdmin_Is403() {
        var r = await ShipEndpoints.HandleGlb(Service(), isAdmin: false, key: "ChickenOne", CancellationToken.None);
        Assert.Equal(403, r.Status);
        Assert.Null(r.Bytes);
    }

    [Fact]
    public async Task Glb_AdminAvailable_Is200Glb() {
        var r = await ShipEndpoints.HandleGlb(Service(), isAdmin: true, key: "ChickenOne", CancellationToken.None);
        Assert.Equal(200, r.Status);
        Assert.Equal("model/gltf-binary", r.ContentType);
        Assert.NotNull(r.Bytes);
    }

    [Fact]
    public async Task Glb_AdminUnavailable_Is404() {
        var r = await ShipEndpoints.HandleGlb(Service(), isAdmin: true, key: "Henerprise", CancellationToken.None);
        Assert.Equal(404, r.Status);
    }

    [Fact]
    public async Task Glb_AdminBogusKey_Is404() {
        var r = await ShipEndpoints.HandleGlb(Service(), isAdmin: true, key: "../x", CancellationToken.None);
        Assert.Equal(404, r.Status);
    }
}
