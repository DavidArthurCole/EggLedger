package reports

import (
	"reflect"
	"testing"
)

// Golden tests lock the EXACT generated SQL string and args for all eight query
// builders. They were captured against the pre-consolidation implementation and
// must remain byte-identical through the queryBuilder refactor. A whitespace-only
// change here requires updating the golden string AND proving semantic equivalence
// via the behavioral execute/query tests.

func assertQuery(t *testing.T, name, gotQ, wantQ string, gotArgs, wantArgs []any) {
	t.Helper()
	if gotQ != wantQ {
		t.Fatalf("%s query mismatch:\n got: %q\nwant: %q", name, gotQ, wantQ)
	}
	if !reflect.DeepEqual(gotArgs, wantArgs) {
		t.Fatalf("%s args mismatch:\n got: %#v\nwant: %#v", name, gotArgs, wantArgs)
	}
}

func TestQueryBuilders_Golden_AggregateMission(t *testing.T) {
	def := ReportDefinition{Mode: "aggregate", GroupBy: "ship_type", Subject: "missions"}
	q, a := buildAggregateQuery(def, "m.player_id = ?", []any{"EI1"})
	want := `
            SELECT CAST(m.ship AS TEXT), COUNT(*) AS count
            FROM mission m
            WHERE m.player_id = ?
            GROUP BY m.ship
            ORDER BY count DESC`
	assertQuery(t, "aggMission", q, want, a, []any{"EI1"})
}

func TestQueryBuilders_Golden_AggregateArtifact(t *testing.T) {
	def := ReportDefinition{Mode: "aggregate", GroupBy: "rarity", Subject: "artifacts"}
	q, a := buildAggregateQuery(def, "m.player_id = ?", []any{"EI1"})
	want := `
            SELECT CAST(d.rarity AS TEXT), COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE m.player_id = ? AND d.drop_index >= 0
            GROUP BY d.rarity
            ORDER BY count DESC`
	assertQuery(t, "aggArtifact", q, want, a, []any{"EI1"})
}

func TestQueryBuilders_Golden_TimeSeriesMission(t *testing.T) {
	def := ReportDefinition{Mode: "time_series", GroupBy: "time_bucket", TimeBucket: "month", Subject: "missions"}
	q, a := buildTimeSeriesQuery(def, "m.player_id = ?", []any{"EI1"})
	want := `
            SELECT strftime('%Y-%m', datetime(m.start_timestamp, 'unixepoch')) AS bucket, COUNT(*) AS count
            FROM mission m
            WHERE m.player_id = ?
            GROUP BY bucket
            ORDER BY bucket ASC`
	assertQuery(t, "tsMission", q, want, a, []any{"EI1"})
}

func TestQueryBuilders_Golden_TimeSeriesArtifactCustom(t *testing.T) {
	def := ReportDefinition{Mode: "time_series", GroupBy: "time_bucket", TimeBucket: "custom", CustomBucketN: 3, CustomBucketUnit: "month", Subject: "artifacts"}
	q, a := buildTimeSeriesQuery(def, "m.player_id = ?", []any{"EI1"})
	want := `
            SELECT strftime('%Y-%m', datetime(m.start_timestamp, 'unixepoch')) AS bucket, COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE m.player_id = ? AND d.drop_index >= 0 AND m.start_timestamp >= strftime('%s', 'now', ?)
            GROUP BY bucket
            ORDER BY bucket ASC`
	assertQuery(t, "tsArtifactCustom", q, want, a, []any{"EI1", "-3 months"})
}

func TestQueryBuilders_Golden_PivotMission(t *testing.T) {
	def := ReportDefinition{Mode: "aggregate", GroupBy: "ship_type", SecondaryGroupBy: "duration_type"}
	q, a, err := buildPivotQuery(def, "m.player_id = ?", []any{"EI1"})
	if err != nil {
		t.Fatal(err)
	}
	want := `
            SELECT CAST(m.ship AS TEXT), CAST(m.duration_type AS TEXT), COUNT(*) AS count
            FROM mission m
            WHERE m.player_id = ?
            GROUP BY m.ship, m.duration_type
            ORDER BY m.ship, m.duration_type`
	assertQuery(t, "pivotMission", q, want, a, []any{"EI1"})
}

func TestQueryBuilders_Golden_PivotArtifact(t *testing.T) {
	def := ReportDefinition{Mode: "aggregate", GroupBy: "rarity", SecondaryGroupBy: "tier"}
	q, a, err := buildPivotQuery(def, "m.player_id = ?", []any{"EI1"})
	if err != nil {
		t.Fatal(err)
	}
	want := `
            SELECT CAST(d.rarity AS TEXT), CAST(d.level AS TEXT), COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE m.player_id = ? AND d.drop_index >= 0
            GROUP BY d.rarity, d.level
            ORDER BY d.rarity, d.level`
	assertQuery(t, "pivotArtifact", q, want, a, []any{"EI1"})
}

func TestQueryBuilders_Golden_TimePivotMission(t *testing.T) {
	def := ReportDefinition{Mode: "time_series", SecondaryGroupBy: "ship_type", TimeBucket: "month"}
	q, a, err := BuildTimePivotQuery(def, "m.player_id = ?", []any{"EI1"})
	if err != nil {
		t.Fatal(err)
	}
	want := `
            SELECT strftime('%Y-%m', datetime(m.start_timestamp, 'unixepoch')) AS bucket, CAST(m.ship AS TEXT) AS grp, COUNT(*) AS count
            FROM mission m
            WHERE m.player_id = ?
            GROUP BY bucket, grp
            ORDER BY bucket ASC, grp ASC`
	assertQuery(t, "timePivotMission", q, want, a, []any{"EI1"})
}

func TestQueryBuilders_Golden_TimePivotArtifactCustom(t *testing.T) {
	def := ReportDefinition{Mode: "time_series", SecondaryGroupBy: "artifact_name", TimeBucket: "custom", CustomBucketN: 2, CustomBucketUnit: "week"}
	q, a, err := BuildTimePivotQuery(def, "m.player_id = ?", []any{"EI1"})
	if err != nil {
		t.Fatal(err)
	}
	want := `
            SELECT strftime('%Y-%W', datetime(m.start_timestamp, 'unixepoch')) AS bucket, CAST(d.artifact_id AS TEXT) AS grp, COUNT(*) AS count
            FROM artifact_drops d
            JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
            WHERE m.player_id = ? AND d.drop_index >= 0 AND m.start_timestamp >= strftime('%s', 'now', ?)
            GROUP BY bucket, grp
            ORDER BY bucket ASC, grp ASC`
	assertQuery(t, "timePivotArtifactCustom", q, want, a, []any{"EI1", "-14 days"})
}

func TestQueryBuilders_Golden_WeightedAggregate(t *testing.T) {
	def := ReportDefinition{Subject: "artifacts", Mode: "aggregate", GroupBy: "ship_type"}
	q, a := BuildWeightedAggregateQuery(def, "m.player_id = ?", []any{"EI1"}, "d.artifact_id IN (?, ?)", []any{1, 2})
	want := `
        SELECT CAST(m.ship AS TEXT), CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),
               SUM(CASE WHEN m.nominal_capacity > 0 AND m.capacity > 0
                        THEN CAST(m.nominal_capacity AS REAL) / CAST(m.capacity AS REAL)
                        ELSE 1.0 END) AS cap_weight
        FROM artifact_drops d
        JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
        WHERE m.player_id = ? AND d.artifact_id IN (?, ?) AND d.drop_index >= 0
        GROUP BY m.ship, d.artifact_id, d.level
        ORDER BY cap_weight DESC`
	assertQuery(t, "wAgg", q, want, a, []any{"EI1", 1, 2})
}

func TestQueryBuilders_Golden_WeightedPivot(t *testing.T) {
	def := ReportDefinition{Subject: "artifacts", Mode: "aggregate", GroupBy: "ship_type", SecondaryGroupBy: "duration_type"}
	q, a, err := BuildWeightedPivotQuery(def, "m.player_id = ?", []any{"EI1"}, "d.artifact_id IN (?)", []any{1})
	if err != nil {
		t.Fatal(err)
	}
	want := `
        SELECT CAST(m.ship AS TEXT), CAST(m.duration_type AS TEXT), CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),
               SUM(CASE WHEN m.nominal_capacity > 0 AND m.capacity > 0
                        THEN CAST(m.nominal_capacity AS REAL) / CAST(m.capacity AS REAL)
                        ELSE 1.0 END) AS cap_weight
        FROM artifact_drops d
        JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
        WHERE m.player_id = ? AND d.artifact_id IN (?) AND d.drop_index >= 0
        GROUP BY m.ship, m.duration_type, d.artifact_id, d.level
        ORDER BY m.ship, m.duration_type`
	assertQuery(t, "wPivot", q, want, a, []any{"EI1", 1})
}

func TestQueryBuilders_Golden_WeightedTimeSeriesCustom(t *testing.T) {
	def := ReportDefinition{Subject: "artifacts", Mode: "time_series", TimeBucket: "custom", CustomBucketN: 4, CustomBucketUnit: "day"}
	q, a := BuildWeightedTimeSeriesQuery(def, "m.player_id = ?", []any{"EI1"}, "d.artifact_id IN (?)", []any{1})
	want := `
        SELECT strftime('%Y-%m-%d', datetime(m.start_timestamp, 'unixepoch')) AS bucket, CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),
               SUM(CASE WHEN m.nominal_capacity > 0 AND m.capacity > 0
                        THEN CAST(m.nominal_capacity AS REAL) / CAST(m.capacity AS REAL)
                        ELSE 1.0 END) AS cap_weight
        FROM artifact_drops d
        JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
        WHERE m.player_id = ? AND d.artifact_id IN (?) AND d.drop_index >= 0 AND m.start_timestamp >= strftime('%s', 'now', ?)
        GROUP BY bucket, d.artifact_id, d.level
        ORDER BY bucket ASC`
	assertQuery(t, "wTimeSeriesCustom", q, want, a, []any{"EI1", 1, "-4 days"})
}

func TestQueryBuilders_Golden_WeightedTimeSeries(t *testing.T) {
	def := ReportDefinition{Subject: "artifacts", Mode: "time_series", TimeBucket: "month"}
	q, a := BuildWeightedTimeSeriesQuery(def, "m.player_id = ?", []any{"EI1"}, "d.artifact_id IN (?)", []any{1})
	want := `
        SELECT strftime('%Y-%m', datetime(m.start_timestamp, 'unixepoch')) AS bucket, CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),
               SUM(CASE WHEN m.nominal_capacity > 0 AND m.capacity > 0
                        THEN CAST(m.nominal_capacity AS REAL) / CAST(m.capacity AS REAL)
                        ELSE 1.0 END) AS cap_weight
        FROM artifact_drops d
        JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
        WHERE m.player_id = ? AND d.artifact_id IN (?) AND d.drop_index >= 0
        GROUP BY bucket, d.artifact_id, d.level
        ORDER BY bucket ASC`
	assertQuery(t, "wTimeSeries", q, want, a, []any{"EI1", 1})
}

func TestQueryBuilders_Golden_WeightedTimePivot(t *testing.T) {
	def := ReportDefinition{Subject: "artifacts", Mode: "time_series", TimeBucket: "month", SecondaryGroupBy: "ship_type"}
	q, a, err := BuildWeightedTimePivotQuery(def, "m.player_id = ?", []any{"EI1"}, "d.artifact_id IN (?)", []any{1})
	if err != nil {
		t.Fatal(err)
	}
	want := `
        SELECT strftime('%Y-%m', datetime(m.start_timestamp, 'unixepoch')) AS bucket, CAST(m.ship AS TEXT) AS grp, CAST(d.artifact_id AS INTEGER), CAST(d.level AS INTEGER),
               SUM(CASE WHEN m.nominal_capacity > 0 AND m.capacity > 0
                        THEN CAST(m.nominal_capacity AS REAL) / CAST(m.capacity AS REAL)
                        ELSE 1.0 END) AS cap_weight
        FROM artifact_drops d
        JOIN mission m ON d.mission_id = m.mission_id AND d.player_id = m.player_id
        WHERE m.player_id = ? AND d.artifact_id IN (?) AND d.drop_index >= 0
        GROUP BY bucket, grp, d.artifact_id, d.level
        ORDER BY bucket ASC, grp ASC`
	assertQuery(t, "wTimePivot", q, want, a, []any{"EI1", 1})
}
