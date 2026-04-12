#!/bin/bash
# sync-ledger-data.sh - Regenerate ledgerdata/ledger-display-data-min.json from source.
# Usage: bash scripts/sync-ledger-data.sh
# Run this after editing ledgerdata/ledger-display-data.json.

set -euo pipefail

RED='\033[0;31m'
GRN='\033[0;32m'
NC='\033[0m'

info() { echo -e "${GRN}[INFO]${NC}  $1"; }
die()  { echo -e "${RED}[ERR]${NC}   $1"; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LEDGER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

SRC="$LEDGER_DIR/ledgerdata/ledger-display-data.json"
OUT="$LEDGER_DIR/ledgerdata/ledger-display-data-min.json"

[ -f "$SRC" ] || die "ledger-display-data.json not found at $SRC"

command -v python3 &>/dev/null || die "python3 not found."

info "Minifying $SRC -> $OUT..."
python3 "$SCRIPT_DIR/minimize.py" "$SRC" "$OUT"

info "Done."
info "  ledgerdata/ledger-display-data-min.json updated"
