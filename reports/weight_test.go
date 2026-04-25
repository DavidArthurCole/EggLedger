package reports_test

import (
	"testing"

	"github.com/DavidArthurCole/EggLedger/reports"
)

func TestClassifyWeight(t *testing.T) {
	cases := []struct {
		name string
		def  reports.ReportDefinition
		want string
	}{
		{
			name: "ships aggregate no filters - LOW",
			def:  reports.ReportDefinition{Subject: "ships", Mode: "aggregate", GroupBy: "ship_type"},
			want: "LOW",
		},
		{
			name: "artifacts aggregate with date filter - LOW",
			def: reports.ReportDefinition{
				Subject: "artifacts", Mode: "aggregate", GroupBy: "rarity",
				Filters: reports.ReportFilters{And: []reports.FilterCondition{{TopLevel: "launchDT", Op: ">", Val: "1700000000"}}},
			},
			want: "LOW",
		},
		{
			name: "artifacts aggregate with artifact-scope filter - MEDIUM",
			def: reports.ReportDefinition{
				Subject: "artifacts", Mode: "aggregate", GroupBy: "rarity",
				Filters: reports.ReportFilters{And: []reports.FilterCondition{{TopLevel: "artifact_rarity", Op: ">=", Val: "2"}}},
			},
			want: "MEDIUM",
		},
		{
			name: "time_series 2 months - MEDIUM",
			def: reports.ReportDefinition{
				Subject: "ships", Mode: "time_series", GroupBy: "time_bucket",
				TimeBucket: "custom", CustomBucketN: 60, CustomBucketUnit: "day",
			},
			want: "MEDIUM",
		},
		{
			name: "time_series 6 months - HEAVY",
			def: reports.ReportDefinition{
				Subject: "ships", Mode: "time_series", GroupBy: "time_bucket",
				TimeBucket: "custom", CustomBucketN: 6, CustomBucketUnit: "month",
			},
			want: "HEAVY",
		},
		{
			name: "artifacts time_series no date bound - HEAVY",
			def:  reports.ReportDefinition{Subject: "artifacts", Mode: "time_series", GroupBy: "time_bucket", TimeBucket: "month"},
			want: "HEAVY",
		},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			got := reports.ClassifyWeight(tc.def)
			if got != tc.want {
				t.Errorf("ClassifyWeight() = %q, want %q", got, tc.want)
			}
		})
	}
}
