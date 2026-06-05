package reports

import "testing"

func TestConditionToSQLDrops(t *testing.T) {
	cases := []struct {
		name      string
		cond      FilterCondition
		wantSub   string // substring expected in clause
		wantArgs  []any
		wantEmpty bool
	}{
		{
			name:     "specific variant contains",
			cond:     FilterCondition{TopLevel: "drops", Op: "c", Val: "12_3_2_4.5"},
			wantSub:  "EXISTS (SELECT 1 FROM artifact_drops WHERE mission_id = m.mission_id AND player_id = m.player_id AND artifact_id = ? AND level = ? AND rarity = ?)",
			wantArgs: []any{"12", "3", "2"},
		},
		{
			name:     "any legendary",
			cond:     FilterCondition{TopLevel: "drops", Op: "c", Val: "%_%_3_%"},
			wantSub:  "EXISTS (SELECT 1 FROM artifact_drops WHERE mission_id = m.mission_id AND player_id = m.player_id AND rarity = ?)",
			wantArgs: []any{"3"},
		},
		{
			name:     "family any, does not contain",
			cond:     FilterCondition{TopLevel: "drops", Op: "dnc", Val: "12_%_%_%"},
			wantSub:  "NOT EXISTS (SELECT 1 FROM artifact_drops WHERE mission_id = m.mission_id AND player_id = m.player_id AND artifact_id = ?)",
			wantArgs: []any{"12"},
		},
		{
			name:     "all wildcard contains",
			cond:     FilterCondition{TopLevel: "drops", Op: "c", Val: "%_%_%_%"},
			wantSub:  "EXISTS (SELECT 1 FROM artifact_drops WHERE mission_id = m.mission_id AND player_id = m.player_id)",
			wantArgs: nil,
		},
		{
			name:      "empty value yields no clause",
			cond:      FilterCondition{TopLevel: "drops", Op: "c", Val: ""},
			wantEmpty: true,
		},
		{
			name:      "unknown op yields no clause",
			cond:      FilterCondition{TopLevel: "drops", Op: "x", Val: "12_%_%_%"},
			wantEmpty: true,
		},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			clause, args := conditionToSQL(tc.cond)
			if tc.wantEmpty {
				if clause != "" {
					t.Fatalf("expected empty clause, got %q", clause)
				}
				return
			}
			if clause != tc.wantSub {
				t.Fatalf("clause mismatch:\n got: %q\nwant: %q", clause, tc.wantSub)
			}
			if len(args) != len(tc.wantArgs) {
				t.Fatalf("args len: got %v want %v", args, tc.wantArgs)
			}
			for i := range args {
				if args[i] != tc.wantArgs[i] {
					t.Fatalf("arg %d: got %v want %v", i, args[i], tc.wantArgs[i])
				}
			}
		})
	}
}
