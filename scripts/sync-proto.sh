#!/bin/bash
# sync-proto.sh - Sync ei.proto and API version constants from a local EggIncProtos checkout.
# Usage: bash scripts/sync-proto.sh <EggIncProtos-dir>
# Copies ei.proto, parses the latest commit message for ClientVersion/AppVersion/Build,
# updates api/request.go, then regenerates ei/ei.pb.go via make protobuf.

set -euo pipefail

RED='\033[0;31m'
GRN='\033[0;32m'
YEL='\033[1;33m'
NC='\033[0m'

info() { echo -e "${GRN}[INFO]${NC}  $1"; }
warn() { echo -e "${YEL}[WARN]${NC}  $1"; }
die()  { echo -e "${RED}[ERR]${NC}   $1"; exit 1; }

PROTOS_DIR="${1:?Usage: sync-proto.sh <EggIncProtos-dir>}"
PROTOS_DIR="$(cd "$PROTOS_DIR" && pwd)"
LEDGER_DIR="$(cd "$(dirname "$0")/.." && pwd)"

command -v go             &>/dev/null || die "go not found. Install: https://go.dev/dl/"
# go install puts binaries in GOPATH/bin, which isn't always on PATH.
export PATH="$PATH:$(go env GOPATH)/bin"
command -v protoc         &>/dev/null || die "protoc not found. Install: https://github.com/protocolbuffers/protobuf/releases"
command -v protoc-gen-go  &>/dev/null || die "protoc-gen-go not found. Install: go install google.golang.org/protobuf/cmd/protoc-gen-go@latest"
command -v gofmt          &>/dev/null || die "gofmt not found. Is Go installed?"

# Parse version constants from the latest commit message.
# Expected format: "ClientVersion: 71, AppVersion: 1.35.5, Build: 111334"
COMMIT_MSG=$(git -C "$PROTOS_DIR" log -1 --format="%s")
info "Latest EggIncProtos commit: $COMMIT_MSG"

CLIENT_VER=$(echo "$COMMIT_MSG" | sed -n 's/.*ClientVersion: \([0-9]*\).*/\1/p')
APP_VER=$(echo    "$COMMIT_MSG" | sed -n 's/.*AppVersion: \([0-9.]*\).*/\1/p')
BUILD=$(echo      "$COMMIT_MSG" | sed -n 's/.*Build: \([0-9]*\).*/\1/p')

[ -n "$CLIENT_VER" ] || die "Could not parse ClientVersion from commit message: $COMMIT_MSG"
[ -n "$APP_VER"    ] || die "Could not parse AppVersion from commit message: $COMMIT_MSG"
[ -n "$BUILD"      ] || die "Could not parse Build from commit message: $COMMIT_MSG"

info "Parsed: ClientVersion=$CLIENT_VER  AppVersion=$APP_VER  Build=$BUILD"

# Check whether the proto or constants actually changed.
CURRENT_MSG=$(git -C "$LEDGER_DIR" log -1 --format="%s" -- ei/ei.proto 2>/dev/null || true)
if [ "$CURRENT_MSG" = "$COMMIT_MSG" ]; then
    warn "ei/ei.proto is already up to date ($COMMIT_MSG). Use 'git pull' in $PROTOS_DIR to fetch newer commits."
fi

# Copy proto file and inject the go_package option (absent in the upstream repo).
info "Copying ei.proto -> ei/ei.proto..."
cp "$PROTOS_DIR/ei.proto" "$LEDGER_DIR/ei/ei.proto"
sed -i 's|^package ei;$|package ei;\n\noption go_package = "github.com/DavidArthurCole/EggLedger/ei";|' \
    "$LEDGER_DIR/ei/ei.proto"

# Patch api/request.go constants.
# Use plain sed replacement then gofmt to restore const-block alignment.
info "Patching api/request.go..."
REQUEST_GO="$LEDGER_DIR/api/request.go"
sed -i "s|AppVersion[[:space:]]*=[[:space:]]*\"[^\"]*\"|AppVersion = \"$APP_VER\"|"          "$REQUEST_GO"
sed -i "s|AppBuild[[:space:]]*=[[:space:]]*\"[^\"]*\"|AppBuild = \"$BUILD\"|"                "$REQUEST_GO"
sed -i "s|ClientVersion[[:space:]]*=[[:space:]]*[0-9][0-9]*|ClientVersion = $CLIENT_VER|"   "$REQUEST_GO"
gofmt -w "$REQUEST_GO"

# Regenerate ei/ei.pb.go.
info "Regenerating ei/ei.pb.go..."
cd "$LEDGER_DIR"
make protobuf

info "Done."
info "  ei/ei.proto      updated from EggIncProtos"
info "  api/request.go   ClientVersion=$CLIENT_VER  AppVersion=$APP_VER  Build=$BUILD"
info "  ei/ei.pb.go      regenerated"
