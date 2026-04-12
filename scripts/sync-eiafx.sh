#!/bin/bash
# sync-eiafx.sh - Fetch the latest ArtifactsConfigurationResponse from the live API.
# Usage: bash scripts/sync-eiafx.sh <EggIncAPITools-dir> <EID>
# Runs EggIncAPITools to fetch eiafx config, then copies the result to
# eiafx/eiafx-config.json and minifies it to eiafx/eiafx-config-min.json.

set -euo pipefail

RED='\033[0;31m'
GRN='\033[0;32m'
NC='\033[0m'

info() { echo -e "${GRN}[INFO]${NC}  $1"; }
die()  { echo -e "${RED}[ERR]${NC}   $1"; exit 1; }

TOOLS_DIR="${1:?Usage: sync-eiafx.sh <EggIncAPITools-dir> <EID>}"
# EID can be passed as second positional arg (make) or via EID env var (npm).
EID="${2:-${EID:-}}"
[ -n "$EID" ] || die "EID not set. Pass as argument or set the EID environment variable."

TOOLS_DIR="$(cd "$TOOLS_DIR" && pwd)"
LEDGER_DIR="$(cd "$(dirname "$0")/.." && pwd)"

command -v node    &>/dev/null || die "node not found. Install Node.js: https://nodejs.org"
command -v python3 &>/dev/null || die "python3 not found."

info "Fetching eiafx config for $EID..."
(cd "$TOOLS_DIR" && node index.js "$EID")

EIAFX_JSON="$TOOLS_DIR/eiafx.json"
[ -f "$EIAFX_JSON" ] || die "Expected $EIAFX_JSON not found after tool run."

info "Copying to eiafx/eiafx-config.json..."
cp "$EIAFX_JSON" "$LEDGER_DIR/eiafx/eiafx-config.json"

info "Minifying to eiafx/eiafx-config-min.json..."
python3 "$LEDGER_DIR/scripts/minimize.py" "$EIAFX_JSON" "$LEDGER_DIR/eiafx/eiafx-config-min.json"

info "Done."
info "  eiafx/eiafx-config.json     updated"
info "  eiafx/eiafx-config-min.json updated"
