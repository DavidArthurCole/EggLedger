# Contributing to EggLedger

## 1. Repo structure

Most logic lives in focused Go packages. `package main` (the repo root) is kept
small - it owns only the window/HTTP setup, the JS-Go binding registrations, the
shared `_storage` instance + dependency-injection wiring, and the fetch pipeline.

### Root-level Go files (`package main`)

| File | Responsibility |
|---|---|
| `main.go` | App entry point, Chrome window setup, all JS-Go binding registrations, the `_storage` global instance, `Set*` dependency-injection wiring, `reportDefToRow`/`rowToReportDef` |
| `fetch.go` | Fetch pipeline / worker pool, `runFetchPipeline`, the `AppState` machine, rate-limited mission fetching |
| `data.go` | API + DB cache bridge (`dataInit`, `fetchFirstContactWithContext`, `fetchCompleteMissionWithContext`) |

### Packages

| Package | Responsibility |
|---|---|
| `api/` | HTTP+protobuf client for the Egg Inc API |
| `db/` | SQLite persistence, migrations, gzip-compressed blobs; `backfill.go` (artifact-drop backfill) and `BuildArtifactDropRows` |
| `ei/` | Protobuf types (`ei.pb.go` - generated) and game logic extension methods |
| `eiafx/` | Artifact configuration loader (`eiafx-config-min.json` embedded); `quality.go` exposes `BaseQualityFor` |
| `ledgerdata/` | Display data loader (`ledger-display-data-min.json` embedded, background refresh) |
| `export/` | CSV and XLSX export (`NewMission`, `MissionsToCsv`, `MissionsToXlsx`, export-file management) |
| `xlsxwriter/` | Minimal streaming single-sheet XLSX writer |
| `missionpacking/` | `DatabaseMission`, mission compile/meta helpers, and the lazy nominal-capacity table (`ShipCapacities`) |
| `missionquery/` | Mission/drop/config query binding handlers (storage injected via `SetStorage`) |
| `processlog/` | `ProcessRegistry` and `Process` - structured progress and log tracking for the UI |
| `storage/` | `AppStorage`/`Account` types + settings methods + path helpers (the `_storage` instance itself lives in `main`) |
| `cloudsync/` | Discord OAuth + encrypted cloud blob sync (deps injected via `SetStorage`/`SetCallbacks`) |
| `menno/` | Community drop-rate data: fetch, cache, filter, comparison (deps injected via `SetHost`) |
| `update/` | In-place auto-updater: handshake-based replace flow, version check (deps injected via `SetHost`) |
| `util/` | Generic helpers - time (`UnixToTime`/`TimeToUnix`/`HumanizeTime`), formatting, `RoleFromEB` |
| `reports/`, `reportdb/` | Report query engine and report persistence |
| `platform/` | OS-specific file operations (hide directory, open-in-explorer) |
| `www/` | Embedded frontend (Vue 3 SPA) |

Packages that expose data to the UI take their `main`-owned dependencies through
a small injected interface or setter (e.g. `update.SetHost`, `cloudsync.SetStorage`,
`missionquery.SetStorage`), wired once in `main()`. No package imports `main`.

### Documentation (`docs/`)

| Path | Contents |
|---|---|
| `docs/reports-module.md` | Reports feature primer |
| `docs/filter-design.md` | Mission/report filter design (composite drop values, SQL building) |
| `docs/menno-normalization.md`, `docs/freeze-investigation-2026-04-25.md`, `docs/mobile-porting-guide.md` | Topic notes |
| `docs/superpowers/specs/` | Approved design specs (`YYYY-MM-DD-<topic>-design.md`) |
| `docs/superpowers/plans/` | Implementation plans (`YYYY-MM-DD-<topic>.md`) |

The canonical, always-current architecture reference is `CLAUDE.md` (Architecture
Map + Key Data Flows). Keep both `CLAUDE.md` and this file in sync when moving or
renaming files, directories, or packages.

---

## 2. Prerequisites

```
Go 1.25+
Node.js 22+               (CSS rebuild only - not needed for Go-only changes)
protoc + protoc-gen-go    (proto changes only - ei.pb.go is already generated)
```

Run `scripts/check-deps.sh` to verify your environment has the required tools.

---

## 3. Platform setup

**Windows:** Pure Go build - no MinGW, no C compiler, no WSL needed. `go build .` works natively. EggLedger uses `modernc.org/sqlite` (pure Go), so `CGO_ENABLED=0` is fine.

**macOS/Linux:** Same `go build .`. On macOS and Linux, a Chrome, Chromium, or Firefox installation is required at runtime (not build time) - the app opens an embedded browser window.

---

## 4. Build stages

The three stages are independent. Only run the ones relevant to what you changed.

**1. CSS** - only when Tailwind classes in `www/index.html` or `tailwind.config.js` change:
```
npm run build:css
```
`www/index.css` is generated - never edit it directly.

**2. Protobuf** - only when `ei/ei.proto` changes:
```
make protobuf
```
Requires `protoc` and `protoc-gen-go` on `$PATH`. Regenerates `ei/ei.pb.go`. Never edit `ei/ei.pb.go` directly.

**3. Go binary:**
```
go build .
```

---

## 5. Dev workflow

```bash
DEV_MODE=1 go run .   # enables hot CSS reload
go test ./...         # run all tests
```

---

## 6. Adding game content

When Auxbrain adds a new artifact, ship, or mission target:

1. **eiafx config** - update `eiafx/eiafx-config-min.json` via `scripts/sync-eiafx.sh` (requires EggIncAPITools and a valid EID)
2. **Display data** - add the entry to `ledgerdata/ledger-display-data.json`, then run `scripts/sync-ledger-data.sh` to regenerate the embedded `ledgerdata/ledger-display-data-min.json`

Sections to update in `ledgerdata/ledger-display-data.json`:

- New artifact effect strings: `artifactEffects`
- New ship: `shipNames`
- New artifact target: `artifactTargets`
- New player rank tier: `farmerRoles`

---

## 7. CI

Single workflow: `.github/workflows/build-artifacts.yml` - triggers on every push. Build matrix: `linux` (ubuntu), `mac` (macos), `mac-arm64` (macos), `windows` (cross-compiled from ubuntu - pure Go, no MinGW). Releases are created by `.github/workflows/create-release.yml` on tag push.

---

## 8. PR conventions

- Branch off `master`
- Run `go build . && go test ./...` before opening
- Keep PRs focused: one logical change per PR
- Describe what changed and why in the PR body
- DB migrations are append-only: add a new numbered file in `db/migrations/` and increment `_schemaVersion` in `db/migrate.go`. Never modify existing migration files.
