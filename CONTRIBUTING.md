# Contributing to EggLedger

## 1. Repo structure

### Root-level Go files (`package main`)

| File | Responsibility |
|---|---|
| `main.go` | App entry point, Chrome window setup, all JS-Go bindings, fetch/export pipeline orchestration |
| `data.go` | API + DB cache bridge (`fetchFirstContactWithContext`, `fetchCompleteMissionWithContext`) |
| `export.go` | CSV and XLSX export logic |
| `fetch.go` | Fetch pipeline worker, `runFetchPipeline`, rate-limited mission fetching |
| `missionpacking.go` | `DatabaseMission` struct, `compileMissionInformation()`, capacity analysis helpers |
| `missionquery.go` | Query handlers for mission data, drop info, and duration configs |
| `processlog.go` | `ProcessRegistry` and `Process` - structured progress and log tracking for UI |
| `storage.go` | `AppStorage` struct with mutex-safe getters/setters, persists to `internal/storage.json` |
| `mennodata.go` | Community drop-rate data: fetch, cache, filter |
| `version.go` | `checkForUpdates()`, polls GitHub releases API |
| `utils.go` | Time helpers, `RoleFromEB()` |

### Packages

| Package | Responsibility |
|---|---|
| `api/` | HTTP+protobuf client for the Egg Inc API |
| `db/` | SQLite persistence, migrations, gzip-compressed blob storage |
| `ei/` | Protobuf types (`ei.pb.go` - generated) and game logic extension methods |
| `eiafx/` | Artifact configuration loader (`eiafx-config-min.json` embedded) |
| `ledgerdata/` | Display data loader (`ledger-display-data-min.json` embedded, background refresh) |
| `platform/` | OS-specific file operations (hide directory, open-in-explorer) |
| `www/` | Embedded frontend (Vue 3 SPA) |

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
2. **Display data** - add the entry to `ledger-display-data.json`, then run `scripts/sync-ledger-data.sh` to regenerate the embedded `ledgerdata/ledger-display-data-min.json`

Sections to update in `ledger-display-data.json`:

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
