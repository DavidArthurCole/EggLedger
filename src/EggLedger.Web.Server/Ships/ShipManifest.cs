namespace EggLedger.Web.Server.Ships;

public sealed record ShipBBox(double[] Min, double[] Max);

public sealed record ShipManifestEntry(string File, string Sha256, ShipBBox Bbox);

public sealed record ShipManifest(
    string Version,
    string? GeneratedFromBuild,
    IReadOnlyDictionary<string, ShipManifestEntry> Ships);
