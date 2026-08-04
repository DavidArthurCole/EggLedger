# EggLedger desktop packaging

Photino desktop host. Self-contained, single-file publish per RID: win-x64,
linux-x64, osx-x64, osx-arm64.

## Version

MinVer (wired in the repo-root `Directory.Build.props`) derives `<Version>`,
`<AssemblyVersion>`, `<FileVersion>`, and `<InformationalVersion>` from the
nearest `git describe` tag (prefix `v`) across every project. The self-updater
reads `InformationalVersion` at runtime to compare against the latest GitHub
release. Bump the version by pushing a new `vX.Y.Z` tag, not by editing a
project file.

## Publish commands (self-contained, single-file, per RID)

Run from the repo root. Each produces a single self-contained exe under
`dist/<rid>/`. The `EggLedger.Web` RCL's Razor/wwwroot content is zipped into
`EggLedger.Desktop.wwwroot.zip` as an embedded resource at build time
(`ComposeWwwrootZip` target) and extracted to disk on first run, rather than
published as loose static web assets.

```bash
dotnet publish EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r win-x64 --self-contained -o dist/win-x64
dotnet publish EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r linux-x64 --self-contained -o dist/linux-x64
dotnet publish EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r osx-x64 --self-contained -o dist/osx-x64
dotnet publish EggLedger.Desktop/EggLedger.Desktop.csproj -c Release -r osx-arm64 --self-contained -o dist/osx-arm64
```

The csproj sets `RuntimeIdentifiers`, `SelfContained`, and `PublishSingleFile`,
so the `-r <rid> --self-contained` form is all each command needs. Disable
single-file per-OS if a packager needs a loose output tree: add
`-p:PublishSingleFile=false`.

## Notes

- The Windows icon (`icon.ico`, embedded via `<ApplicationIcon>`) and the native
  window icon (`SetIconFile` in `Program.cs`) are both generated from `icon-64.png`.
- Photino native libs land per-RID in the output; with single-file they are
  self-extracted at first run.
- Cloud sync and Menno work on desktop because the HttpClient base address is set
  to the production sync host (`https://eggledger.davidarthurcole.me/`) in
  `Program.cs`.
