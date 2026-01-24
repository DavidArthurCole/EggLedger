package main

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"time"

	version "github.com/hashicorp/go-version"
	log "github.com/sirupsen/logrus"
)

const (
	_githubRepo          = "DavidArthurCole/EggLedger"
	_updateCheckInterval = time.Hour * 12
)

func checkForUpdates() (newVersion string, newReleaseNotes string, err error) {
	wrap := func(err error) error {
		return fmt.Errorf("failed to check for new version: %w", err)
	}
	runningVersion, err := version.NewVersion(_appVersion)
	if err != nil {
		err = fmt.Errorf("failed to parse running version %s: %w", _appVersion, err)
		return "", "", wrap(err)
	}

	_storage.Lock()
	lastUpdateCheckAt := _storage.LastUpdateCheckAt
	knownLatestTag := _storage.KnownLatestVersion
	knownLatestReleaseNotes := _storage.KnownLatestReleaseNotes
	_storage.Unlock()
	if knownLatestTag != "" {
		if knownLatestVersion, err := version.NewVersion(knownLatestTag); err == nil {
			if knownLatestVersion.GreaterThan(runningVersion) {
				// A known new version is already stored, skip remote check.
				return knownLatestTag, knownLatestReleaseNotes, nil
			}
		} else {
			log.Warnf("storage: failed to parse known_latest_version %s: %s", knownLatestTag, err)
		}
	}

	if time.Since(lastUpdateCheckAt) < _updateCheckInterval {
		log.Infof("%s since last update check, skipping", time.Since(lastUpdateCheckAt))
		return "skip", "", nil
	}

	latestTag, latestReleaseNotes, err := getLatestTag()
	if err != nil {
		return "", "", wrap(err)
	}
	log.Infof("latest tag: %s", latestTag)
	latestVersion, err := version.NewVersion(latestTag)
	if err != nil {
		err = fmt.Errorf("failed to parse latest version %s: %w", latestTag, err)
		return "", "", wrap(err)
	}

	_storage.SetUpdateCheck(latestTag, latestReleaseNotes)

	if runningVersion.LessThan(latestVersion) {
		return latestTag, latestReleaseNotes, nil
	}
	return "", "", nil
}

func getLatestTag() (string, string, error) {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	url := fmt.Sprintf("https://api.github.com/repos/%s/releases/latest", _githubRepo)
	req, err := http.NewRequestWithContext(ctx, "GET", url, nil)
	if err != nil {
		return "", "", fmt.Errorf("creating request for %s: %w", url, err)
	}

	client := http.DefaultClient
	resp, err := client.Do(req)
	if err != nil {
		return "", "", fmt.Errorf("GET %s: %w", url, err)
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", "", fmt.Errorf("reading response body for %s: %#v: %w", url, string(body), err)
	}

	if resp.StatusCode != http.StatusOK {
		return "", "", fmt.Errorf("GET %s: HTTP %d: %#v", url, resp.StatusCode, string(body))
	}

	var release struct {
		TagName string `json:"tag_name"`
		Body    string `json:"body"`
	}
	err = json.Unmarshal(body, &release)
	if err != nil {
		return "", "", fmt.Errorf("parsing JSON for %s: %#v: %w", url, string(body), err)
	}

	if release.TagName == "" {
		return "", "", fmt.Errorf("GET %s: tag_name is empty: %#v", url, string(body))
	}

	return release.TagName, release.Body, nil
}
