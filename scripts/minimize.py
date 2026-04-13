#!/usr/bin/env python3
# minimize.py - Minify a JSON file by stripping whitespace.
# Usage: python3 scripts/minimize.py <input> <output>

import json
import sys

if len(sys.argv) != 3:
    print(f"Usage: {sys.argv[0]} <input> <output>", file=sys.stderr)
    sys.exit(1)

input_file, output_file = sys.argv[1], sys.argv[2]

try:
    with open(input_file) as f:
        data = json.load(f)
    with open(output_file, "w") as f:
        json.dump(data, f, separators=(",", ":"))
except Exception as e:
    print(f"Error: {e}", file=sys.stderr)
    sys.exit(1)
