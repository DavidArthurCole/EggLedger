#!/usr/bin/env bash
# Minifies eiafx/eiafx-data.json -> eiafx/eiafx-data-min.json, retaining only
# fields required for crafting weight computation. Run from the repo root.
set -euo pipefail

INPUT="eiafx/eiafx-data.json"
OUTPUT="eiafx/eiafx-data-min.json"

if [[ ! -f "$INPUT" ]]; then
  echo "Error: $INPUT not found." >&2
  exit 1
fi

python3 - "$INPUT" "$OUTPUT" << 'PYTHON_EOF'
import json, sys

with open(sys.argv[1], "r") as f:
    data = json.load(f)

minified = {
    "families": [
        {
            "id": family["id"],
            "name": family["name"],
            "child_afx_ids": family["child_afx_ids"],
            "tiers": [
                {
                    "afx_id": tier["afx_id"],
                    "afx_level": tier["afx_level"],
                    "recipe": (
                        [
                            {
                                "afx_id": ingredient["afx_id"],
                                "afx_level": ingredient["afx_level"],
                                "count": ingredient["count"],
                            }
                            for ingredient in tier.get("recipe", {}).get("ingredients", [])
                        ]
                        if tier.get("recipe") and isinstance(tier.get("recipe"), dict)
                        else None
                    ),
                }
                for tier in family["tiers"]
            ],
        }
        for family in data["artifact_families"]
    ]
}

with open(sys.argv[2], "w") as f:
    json.dump(minified, f, separators=(",", ":"))

print(f"Written: {sys.argv[2]}")
PYTHON_EOF
