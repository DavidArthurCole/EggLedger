package reportdb

import (
	"context"
	"database/sql"
	"encoding/json"
	"fmt"
	"time"

	"github.com/pkg/errors"
)

// ReportRow is the raw DB representation. Filters is stored as JSON.
type ReportRow struct {
	Id               string
	AccountId        string
	Name             string
	Subject          string
	Mode             string
	DisplayMode      string
	GroupBy          string
	TimeBucket       sql.NullString
	CustomBucketN    sql.NullInt64
	CustomBucketUnit sql.NullString
	FiltersJSON      string
	GridX            int
	GridY            int
	GridW            int
	GridH            int
	Weight           string
	Color            string
	Description      string
	ChartType        string
	SortOrder int
	CreatedAt int64
	UpdatedAt int64
	ValueFilterOp string
	ValueFilterThreshold float64
	GroupId string
	NormalizeBy sql.NullString
	LabelColors string
	SecondaryGroupBy string
}

func InsertReport(ctx context.Context, r ReportRow) error {
	action := fmt.Sprintf("insert report %s", r.Id)
	filtersJSON, err := json.Marshal(json.RawMessage(r.FiltersJSON))
	if err != nil {
		return errors.Wrap(err, action)
	}
	now := time.Now().Unix()
	return DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		_, err := db.ExecContext(ctx, `
            INSERT INTO reports(id, account_id, name, subject, mode, display_mode, group_by,
                time_bucket, custom_bucket_n, custom_bucket_unit, filters,
                grid_x, grid_y, grid_w, grid_h, weight, color, description, chart_type,
                sort_order, created_at, updated_at, value_filter_op, value_filter_threshold, group_id,
                normalize_by, label_colors, secondary_group_by)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
			r.Id, r.AccountId, r.Name, r.Subject, r.Mode, r.DisplayMode, r.GroupBy,
			r.TimeBucket, r.CustomBucketN, r.CustomBucketUnit, string(filtersJSON),
			r.GridX, r.GridY, r.GridW, r.GridH, r.Weight, r.Color, r.Description, r.ChartType,
			r.SortOrder, now, now, r.ValueFilterOp, r.ValueFilterThreshold, r.GroupId,
			r.NormalizeBy, r.LabelColors, r.SecondaryGroupBy)
		if err != nil {
			return errors.Wrap(err, action)
		}
		return nil
	})
}

func UpdateReport(ctx context.Context, r ReportRow) error {
	action := fmt.Sprintf("update report %s", r.Id)
	filtersJSON, err := json.Marshal(json.RawMessage(r.FiltersJSON))
	if err != nil {
		return errors.Wrap(err, action)
	}
	now := time.Now().Unix()
	return DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		_, err := db.ExecContext(ctx, `
            UPDATE reports SET name=?, subject=?, mode=?, display_mode=?, group_by=?,
                time_bucket=?, custom_bucket_n=?, custom_bucket_unit=?, filters=?,
                grid_x=?, grid_y=?, grid_w=?, grid_h=?, weight=?, color=?,
                description=?, chart_type=?, sort_order=?, updated_at=?,
                value_filter_op=?, value_filter_threshold=?, group_id=?,
                normalize_by=?, label_colors=?, secondary_group_by=?
            WHERE id=?`,
			r.Name, r.Subject, r.Mode, r.DisplayMode, r.GroupBy,
			r.TimeBucket, r.CustomBucketN, r.CustomBucketUnit, string(filtersJSON),
			r.GridX, r.GridY, r.GridW, r.GridH, r.Weight, r.Color,
			r.Description, r.ChartType, r.SortOrder, now,
			r.ValueFilterOp, r.ValueFilterThreshold, r.GroupId,
			r.NormalizeBy, r.LabelColors, r.SecondaryGroupBy,
			r.Id)
		if err != nil {
			return errors.Wrap(err, action)
		}
		return nil
	})
}

func DeleteReport(ctx context.Context, id string) error {
	return DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		_, err := db.ExecContext(ctx, `DELETE FROM reports WHERE id = ?`, id)
		return err
	})
}

func RetrieveReport(ctx context.Context, id string) (*ReportRow, error) {
	var r ReportRow
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		row := db.QueryRowContext(ctx, `SELECT id, account_id, name, subject, mode, display_mode,
            group_by, time_bucket, custom_bucket_n, custom_bucket_unit, filters,
            grid_x, grid_y, grid_w, grid_h, weight, color, description, chart_type,
            sort_order, created_at, updated_at, value_filter_op, value_filter_threshold, group_id,
            normalize_by, label_colors, secondary_group_by
            FROM reports WHERE id = ?`, id)
		return row.Scan(&r.Id, &r.AccountId, &r.Name, &r.Subject, &r.Mode, &r.DisplayMode,
			&r.GroupBy, &r.TimeBucket, &r.CustomBucketN, &r.CustomBucketUnit, &r.FiltersJSON,
			&r.GridX, &r.GridY, &r.GridW, &r.GridH, &r.Weight, &r.Color, &r.Description, &r.ChartType,
			&r.SortOrder, &r.CreatedAt, &r.UpdatedAt, &r.ValueFilterOp, &r.ValueFilterThreshold, &r.GroupId,
			&r.NormalizeBy, &r.LabelColors, &r.SecondaryGroupBy)
	})
	if err != nil {
		return nil, err
	}
	return &r, nil
}

func RetrieveAccountReports(ctx context.Context, accountId string) ([]ReportRow, error) {
	var rows []ReportRow
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		rs, err := db.QueryContext(ctx, `SELECT id, account_id, name, subject, mode, display_mode,
            group_by, time_bucket, custom_bucket_n, custom_bucket_unit, filters,
            grid_x, grid_y, grid_w, grid_h, weight, color, description, chart_type,
            sort_order, created_at, updated_at, value_filter_op, value_filter_threshold, group_id,
            normalize_by, label_colors, secondary_group_by
            FROM reports WHERE account_id = ? ORDER BY sort_order ASC, created_at ASC`, accountId)
		if err != nil {
			return err
		}
		defer rs.Close()
		for rs.Next() {
			var r ReportRow
			if err := rs.Scan(&r.Id, &r.AccountId, &r.Name, &r.Subject, &r.Mode, &r.DisplayMode,
				&r.GroupBy, &r.TimeBucket, &r.CustomBucketN, &r.CustomBucketUnit, &r.FiltersJSON,
				&r.GridX, &r.GridY, &r.GridW, &r.GridH, &r.Weight, &r.Color, &r.Description, &r.ChartType,
				&r.SortOrder, &r.CreatedAt, &r.UpdatedAt, &r.ValueFilterOp, &r.ValueFilterThreshold,
				&r.GroupId, &r.NormalizeBy, &r.LabelColors, &r.SecondaryGroupBy); err != nil {
				return err
			}
			rows = append(rows, r)
		}
		return rs.Err()
	})
	return rows, err
}

func ReorderReports(ctx context.Context, ids []string) error {
	return DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		tx, err := db.BeginTx(ctx, nil)
		if err != nil {
			return err
		}
		defer tx.Rollback()
		for i, id := range ids {
			if _, err := tx.ExecContext(ctx, `UPDATE reports SET sort_order = ? WHERE id = ?`, i, id); err != nil {
				return err
			}
		}
		return tx.Commit()
	})
}
