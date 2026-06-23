# EggLedger desktop packaging

Photino desktop host. Cross-platform publish mirrors the Go CI matrix
(`EggLedger/.github/workflows/ci.yml`): linux, mac, mac-arm64, windows.

## Version

`<Version>`/`<InformationalVersion>` in `EggLedger.Desktop.csproj` is pinned to
`2.1.4` (the Go `VERSION` file). The self-updater reads `InformationalVersion` at
runtime to compare against the latest GitHub release. Bump both alongside the Go
`VERSION` on release.

## Publish commands (self-contained, single-file, per RID)

Run from the repo root. Each produces a runnable output tree (or single exe on
Windows) under `dist/<rid>/`. Self-contained bundles the .NET runtime + the Photino
native lib; single-file folds the native lib into the host via self-extract. The
RCL static web assets (`wwwroot/_content/EggLedger.Web/...` the WebView loads) and
`wwwroot/desktop.html` are published automatically.

```bash
# Windows x64 (verified: produces dist/win-x64/EggLedger.exe, ~78 MB, version 2.1.4)
dotnet publish src/EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r win-x64 --self-contained -o dist/win-x64

# Linux x64 (MANUAL-VERIFY)
dotnet publish src/EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r linux-x64 --self-contained -o dist/linux-x64

# macOS x64 (MANUAL-VERIFY)
dotnet publish src/EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r osx-x64 --self-contained -o dist/osx-x64

# macOS arm64 (MANUAL-VERIFY)
dotnet publish src/EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r osx-arm64 --self-contained -o dist/osx-arm64
```

The csproj sets `RuntimeIdentifiers`, `SelfContained`, `PublishSingleFile`, and
`IncludeNativeLibrariesForSelfExtract`, so the `-r <rid> --self-contained` form is
all each command needs. Disable single-file per-OS if a packager needs a loose
output tree: add `-p:PublishSingleFile=false`.

## Notes

- The Windows icon (`icon.ico`, embedded via `<ApplicationIcon>`) is generated from
  `icon-64.png`. The native window icon (`SetIconFile` in `Program.cs`) uses
  `icon-64.png`, copied to output. macOS bundles use `EggLedger/assets/icons/icon.icns`
  if an app-bundle step is added later (not wired here).
- Photino native libs land per-RID in the output; with single-file they are
  self-extracted at first run. Confirmed for win-x64.
- Cloud sync + Menno work on desktop because the HttpClient base address is set to
  the production sync host (`https://ledgersync.davidarthurcole.me/`) in
  `Program.cs`; Menno's data URL is absolute and independent of that base.
