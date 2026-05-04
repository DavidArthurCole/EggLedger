package main

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"net/http"

	"github.com/pkg/errors"
)

var _mennoProxyURL = "https://ledgersync.davidarthurcole.me/api/v1/menno/submit"

func submitEidToMenno(ctx context.Context, eid string) error {
	action := "submitEidToMenno"
	wrap := func(err error) error { return errors.Wrap(err, action) }

	payload, err := json.Marshal(map[string]string{"eid": eid})
	if err != nil {
		return wrap(err)
	}

	req, err := http.NewRequestWithContext(ctx, http.MethodPost, _mennoProxyURL, bytes.NewReader(payload))
	if err != nil {
		return wrap(err)
	}
	req.Header.Set("Content-Type", "application/json")

	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return wrap(err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return wrap(fmt.Errorf("unexpected status %d", resp.StatusCode))
	}
	return nil
}
