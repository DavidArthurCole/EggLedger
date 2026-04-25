package reports

// FilterCondition mirrors the frontend FilterCondition structure.
type FilterCondition struct {
	TopLevel string `json:"topLevel"`
	Op       string `json:"op"`
	Val      string `json:"val"`
}

// ReportFilters holds the AND and OR filter groups for a report.
type ReportFilters struct {
	And []FilterCondition   `json:"and"`
	Or  [][]FilterCondition `json:"or"`
}

// ReportDefinition is the full configuration for a single report.
type ReportDefinition struct {
	Id               string        `json:"id"`
	AccountId        string        `json:"accountId"`
	Name             string        `json:"name"`
	Subject          string        `json:"subject"`
	Mode             string        `json:"mode"`
	DisplayMode      string        `json:"displayMode"`
	GroupBy          string        `json:"groupBy"`
	TimeBucket       string        `json:"timeBucket"`
	CustomBucketN    int           `json:"customBucketN"`
	CustomBucketUnit string        `json:"customBucketUnit"`
	Filters          ReportFilters `json:"filters"`
	GridX            int           `json:"gridX"`
	GridY            int           `json:"gridY"`
	GridW            int           `json:"gridW"`
	GridH            int           `json:"gridH"`
	Weight           string        `json:"weight"`
	Color            string        `json:"color"`
	Description      string        `json:"description"`
	ChartType        string        `json:"chartType"`
	SortOrder           int           `json:"sortOrder"`
	CreatedAt           int64         `json:"createdAt"`
	UpdatedAt           int64         `json:"updatedAt"`
	ValueFilterOp string `json:"valueFilterOp"`
	ValueFilterThreshold float64 `json:"valueFilterThreshold"`
	GroupId string `json:"groupId"`
	NormalizeBy string `json:"normalizeBy"`
}

// ReportResult is the computed output of executing a report.
type ReportResult struct {
	Labels      []string  `json:"labels"`
	Values      []int64   `json:"values"`
	FloatValues []float64 `json:"floatValues"`
	IsFloat     bool      `json:"isFloat"`
	Weight      string    `json:"weight"`
}
