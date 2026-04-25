package reportdb

import (
	"context"
	"database/sql"
	"fmt"
	"time"

	"github.com/google/uuid"
	"github.com/pkg/errors"
)

// ReportGroupRow is the raw DB representation of a report group.
type ReportGroupRow struct {
	Id        string
	AccountId string
	Name      string
	SortOrder int
	CreatedAt int64
}

func InsertReportGroup(ctx context.Context, r ReportGroupRow) (string, error) {
	action := "insert report group"
	wrap := func(err error) error { return errors.Wrap(err, action) }
	if r.Id == "" {
		r.Id = uuid.NewString()
	}
	r.CreatedAt = time.Now().Unix()
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		_, err := db.ExecContext(ctx,
			`INSERT INTO report_groups(id, account_id, name, sort_order, created_at)
             VALUES (?, ?, ?, ?, ?)`,
			r.Id, r.AccountId, r.Name, r.SortOrder, r.CreatedAt)
		return err
	})
	if err != nil {
		return "", wrap(err)
	}
	return r.Id, nil
}

func UpdateReportGroup(ctx context.Context, r ReportGroupRow) error {
	action := fmt.Sprintf("update report group %s", r.Id)
	wrap := func(err error) error { return errors.Wrap(err, action) }
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		_, err := db.ExecContext(ctx,
			`UPDATE report_groups SET name = ?, sort_order = ? WHERE id = ?`,
			r.Name, r.SortOrder, r.Id)
		return err
	})
	return wrap(err)
}

func DeleteReportGroup(ctx context.Context, id string) error {
	action := fmt.Sprintf("delete report group %s", id)
	wrap := func(err error) error { return errors.Wrap(err, action) }
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		tx, err := db.BeginTx(ctx, nil)
		if err != nil {
			return err
		}
		defer tx.Rollback()
		if _, err := tx.ExecContext(ctx,
			`UPDATE reports SET group_id = '' WHERE group_id = ?`, id); err != nil {
			return err
		}
		if _, err := tx.ExecContext(ctx,
			`DELETE FROM report_groups WHERE id = ?`, id); err != nil {
			return err
		}
		return tx.Commit()
	})
	return wrap(err)
}

func RetrieveAccountGroups(ctx context.Context, accountId string) ([]ReportGroupRow, error) {
	var rows []ReportGroupRow
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		rs, err := db.QueryContext(ctx,
			`SELECT id, account_id, name, sort_order, created_at
             FROM report_groups WHERE account_id = ? ORDER BY sort_order ASC, created_at ASC`, accountId)
		if err != nil {
			return err
		}
		defer rs.Close()
		for rs.Next() {
			var r ReportGroupRow
			if err := rs.Scan(&r.Id, &r.AccountId, &r.Name, &r.SortOrder, &r.CreatedAt); err != nil {
				return err
			}
			rows = append(rows, r)
		}
		return rs.Err()
	})
	return rows, err
}

func RetrieveReportGroup(ctx context.Context, id string) (*ReportGroupRow, error) {
	var r ReportGroupRow
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		row := db.QueryRowContext(ctx,
			`SELECT id, account_id, name, sort_order, created_at
             FROM report_groups WHERE id = ?`, id)
		return row.Scan(&r.Id, &r.AccountId, &r.Name, &r.SortOrder, &r.CreatedAt)
	})
	if err != nil {
		return nil, err
	}
	return &r, nil
}

func RetrieveReportsByGroup(ctx context.Context, groupId string) ([]ReportRow, error) {
	var rows []ReportRow
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		rs, err := db.QueryContext(ctx, `SELECT id, account_id, name, subject, mode, display_mode,
            group_by, time_bucket, custom_bucket_n, custom_bucket_unit, filters,
            grid_x, grid_y, grid_w, grid_h, weight, color, description, chart_type,
            sort_order, created_at, updated_at, value_filter_op, value_filter_threshold, group_id
            FROM reports WHERE group_id = ? ORDER BY sort_order ASC, created_at ASC`, groupId)
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
				&r.GroupId); err != nil {
				return err
			}
			rows = append(rows, r)
		}
		return rs.Err()
	})
	return rows, err
}

func SetReportGroup(ctx context.Context, reportId, groupId string) error {
	action := fmt.Sprintf("set report group %s -> %s", reportId, groupId)
	wrap := func(err error) error { return errors.Wrap(err, action) }
	err := DoReportDBOperation(ctx, func(ctx context.Context, db *sql.DB) error {
		_, err := db.ExecContext(ctx,
			`UPDATE reports SET group_id = ? WHERE id = ?`, groupId, reportId)
		return err
	})
	return wrap(err)
}
