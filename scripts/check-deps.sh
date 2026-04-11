#!/bin/bash
# check-deps.sh — Verify all EggLedger build dependencies are present.
# Usage: bash scripts/check-deps.sh
# Exit code: 0 = all required deps present, 1 = one or more missing.

set -euo pipefail

RED='\033[0;31m'
YEL='\033[1;33m'
GRN='\033[0;32m'
NC='\033[0m'

ok()   { echo -e "${GRN}[OK]${NC}    $1"; }
warn() { echo -e "${YEL}[WARN]${NC}  $1"; }
fail() { echo -e "${RED}[FAIL]${NC}  $1"; FAILED=1; }

FAILED=0

echo "=== EggLedger Dependency Check ==="
echo ""

# --- Go ---
if command -v go &>/dev/null; then
    GO_VER=$(go version | awk '{print $3}' | sed 's/go//')
    GO_MAJOR=$(echo "$GO_VER" | cut -d. -f1)
    GO_MINOR=$(echo "$GO_VER" | cut -d. -f2)
    if [ "$GO_MAJOR" -gt 1 ] || ([ "$GO_MAJOR" -eq 1 ] && [ "$GO_MINOR" -ge 25 ]); then
        ok "Go $GO_VER"
    else
        fail "Go $GO_VER is too old. Need Go 1.25+. Download: https://go.dev/dl/"
    fi
else
    fail "Go not found. Download: https://go.dev/dl/"
fi

# --- Node.js / npm (for CSS build) ---
if command -v node &>/dev/null; then
    NODE_VER=$(node --version)
    ok "Node.js $NODE_VER"
else
    warn "Node.js not found. Required to build CSS (www/index.css). Download: https://nodejs.org"
fi

if command -v npm &>/dev/null; then
    NPM_VER=$(npm --version)
    ok "npm $NPM_VER"
else
    warn "npm not found. Required to build CSS. Comes with Node.js: https://nodejs.org"
fi

# --- protoc (optional — only needed when ei/ei.proto changes) ---
if command -v protoc &>/dev/null; then
    PROTOC_VER=$(protoc --version)
    ok "protoc ($PROTOC_VER) — needed only if ei/ei.proto changes"
else
    warn "protoc not found. Only needed if you change ei/ei.proto. ei/ei.pb.go is already generated."
    warn "  Install: https://github.com/protocolbuffers/protobuf/releases"
fi

if command -v protoc-gen-go &>/dev/null; then
    ok "protoc-gen-go — needed only if ei/ei.proto changes"
else
    warn "protoc-gen-go not found. Only needed if you change ei/ei.proto."
    warn "  Install: go install google.golang.org/protobuf/cmd/protoc-gen-go@latest"
fi

echo ""
if [ "$FAILED" -ne 0 ]; then
    echo -e "${RED}One or more required dependencies are missing. See above for install links.${NC}"
    exit 1
else
    echo -e "${GRN}All required dependencies present. Ready to build.${NC}"
fi
