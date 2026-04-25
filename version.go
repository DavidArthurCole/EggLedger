package main

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"runtime"
	"time"

	version "github.com/hashicorp/go-version"
	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
)

const (
	_githubRepo          = "DavidArthurCole/EggLedger"
	_updateCheckInterval = time.Hour * 12
)

func checkForUpdates() (newVersion string, newReleaseNotes string, err error) {
	wrap := func(err error) error {
		return errors.Wrap(err, "failed to check for new version")
	}
	runningVersion, err := version.NewVersion(_appVersion)
	if err != nil {
		err = errors.Wrapf(err, "failed to parse running version %s", _appVersion)
		return "", "", wrap(err)
	}

	_storage.Lock()
	lastUpdateCheckAt := _storage.LastUpdateCheckAt
	knownLatestTag := _storage.KnownLatestVersion
	knownLatestReleaseNotes := _storage.KnownLatestReleaseNotes
	_storage.Unlock()
	if !_forceUpdateCheck && knownLatestTag != "" {
		if knownLatestVersion, err := version.NewVersion(knownLatestTag); err == nil {
			if knownLatestVersion.GreaterThan(runningVersion) {
				// A known new version is already stored, skip remote check.
				return knownLatestTag, knownLatestReleaseNotes, nil
			}
		} else {
			log.Warnf("storage: failed to parse known_latest_version %s: %s", knownLatestTag, err)
		}
	}

	if !_forceUpdateCheck && time.Since(lastUpdateCheckAt) < _updateCheckInterval {
		log.Infof("%s since last update check, skipping", time.Since(lastUpdateCheckAt))
		return "skip", "", nil
	}

	latestTag, latestReleaseNotes, err := getLatestTag()
	if err != nil {
		return "", "", wrap(err)
	}
	log.Infof("latest stable tag: %s", latestTag)
	latestVersion, err := version.NewVersion(latestTag)
	if err != nil {
		err = errors.Wrapf(err, "failed to parse latest version %s", latestTag)
		return "", "", wrap(err)
	}

	// If running a version newer than the latest stable release (e.g. a pre-release build),
	// fetch all releases including pre-releases to find the true latest.
	if runningVersion.GreaterThan(latestVersion) {
		log.Infof("running version %s is newer than latest stable %s, checking pre-releases", _appVersion, latestTag)
		if preTag, preNotes, preErr := getLatestTagIncludingPreReleases(); preErr != nil {
			log.Warnf("failed to check pre-releases: %s", preErr)
		} else {
			log.Infof("latest pre-release tag: %s", preTag)
			latestTag = preTag
			latestReleaseNotes = preNotes
			if latestVersion, err = version.NewVersion(latestTag); err != nil {
				err = errors.Wrapf(err, "failed to parse latest pre-release version %s", latestTag)
				return "", "", wrap(err)
			}
		}
	}

	_storage.SetUpdateCheck(latestTag, latestReleaseNotes)

	if runningVersion.LessThan(latestVersion) {
		return latestTag, latestReleaseNotes, nil
	}
	return "", "", nil
}

func getLatestTagIncludingPreReleases() (string, string, error) {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	url := fmt.Sprintf("https://api.github.com/repos/%s/releases?per_page=10", _githubRepo)
	req, err := http.NewRequestWithContext(ctx, "GET", url, nil)
	if err != nil {
		return "", "", errors.Wrapf(err, "creating request for %s", url)
	}

	client := http.DefaultClient
	resp, err := client.Do(req)
	if err != nil {
		return "", "", errors.Wrapf(err, "GET %s", url)
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", "", errors.Wrapf(err, "reading response body for %s: %#v", url, string(body))
	}

	if resp.StatusCode != http.StatusOK {
		return "", "", errors.Errorf("GET %s: HTTP %d: %#v", url, resp.StatusCode, string(body))
	}

	var releases []struct {
		TagName string `json:"tag_name"`
		Body    string `json:"body"`
		Draft   bool   `json:"draft"`
	}
	if err = json.Unmarshal(body, &releases); err != nil {
		return "", "", errors.Wrapf(err, "parsing JSON for %s: %#v", url, string(body))
	}

	var highestVersion *version.Version
	var highestTag, highestBody string
	for _, release := range releases {
		if release.Draft {
			continue
		}
		v, parseErr := version.NewVersion(release.TagName)
		if parseErr != nil {
			continue
		}
		if highestVersion == nil || v.GreaterThan(highestVersion) {
			highestVersion = v
			highestTag = release.TagName
			highestBody = release.Body
		}
	}

	if highestTag == "" {
		return "", "", errors.Errorf("GET %s: no valid releases found", url)
	}

	return highestTag, highestBody, nil
}

// expectedAssetName returns the expected binary name for the current platform.
// e.g. "EggLedger_windows_amd64.exe" or "EggLedger_darwin_arm64"
func expectedAssetName() string {
	name := fmt.Sprintf("EggLedger_%s_%s", runtime.GOOS, runtime.GOARCH)
	if runtime.GOOS == "windows" {
		name += ".exe"
	}
	return name
}

// getUpdateAssetURL fetches the GitHub releases API for the given tag and returns
// the browser_download_url for the asset matching the current GOOS/GOARCH.
func getUpdateAssetURL(tag string) (string, error) {
	wrap := func(err error) error { return errors.Wrap(err, "getUpdateAssetURL") }

	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	url := fmt.Sprintf("https://api.github.com/repos/%s/releases/tags/%s", _githubRepo, tag)
	req, err := http.NewRequestWithContext(ctx, "GET", url, nil)
	if err != nil {
		return "", wrap(errors.Wrapf(err, "creating request for %s", url))
	}

	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return "", wrap(errors.Wrapf(err, "GET %s", url))
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", wrap(errors.Wrapf(err, "reading response body for %s", url))
	}

	if resp.StatusCode != http.StatusOK {
		return "", wrap(errors.Errorf("GET %s: HTTP %d: %s", url, resp.StatusCode, string(body)))
	}

	var release map[string]interface{}
	if err := json.Unmarshal(body, &release); err != nil {
		return "", wrap(errors.Wrapf(err, "parsing JSON for %s", url))
	}

	assets, ok := release["assets"].([]interface{})
	if !ok {
		return "", wrap(errors.Errorf("GET %s: assets field missing or not an array", url))
	}

	want := expectedAssetName()
	for _, a := range assets {
		asset, ok := a.(map[string]interface{})
		if !ok {
			continue
		}
		name, _ := asset["name"].(string)
		if name == want {
			downloadURL, _ := asset["browser_download_url"].(string)
			if downloadURL == "" {
				return "", wrap(errors.Errorf("asset %s has empty browser_download_url", want))
			}
			return downloadURL, nil
		}
	}

	return "", wrap(errors.Errorf("no asset named %q found in release %s", want, tag))
}

// countingReader wraps an io.Reader and calls progressCb every ~64KB with
// cumulative (downloaded, total) byte counts.
type countingReader struct {
	r          io.Reader
	downloaded int64
	total      int64
	progressCb func(downloaded, total int64)
	lastReport int64
}

func (cr *countingReader) Read(p []byte) (int, error) {
	n, err := cr.r.Read(p)
	cr.downloaded += int64(n)
	if cr.downloaded-cr.lastReport >= 64*1024 || err == io.EOF {
		cr.progressCb(cr.downloaded, cr.total)
		cr.lastReport = cr.downloaded
	}
	return n, err
}

// downloadUpdate streams the asset at assetURL to destPath, calling progressCb
// every ~64KB with (downloaded, total) bytes. Returns error on non-200 or
// write failure.
func downloadUpdate(assetURL, destPath string, progressCb func(downloaded, total int64)) error {
	wrap := func(err error) error { return errors.Wrap(err, "downloadUpdate") }

	resp, err := http.Get(assetURL) //nolint:noctx
	if err != nil {
		return wrap(errors.Wrapf(err, "GET %s", assetURL))
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return wrap(errors.Errorf("GET %s: HTTP %d", assetURL, resp.StatusCode))
	}

	f, err := os.Create(destPath)
	if err != nil {
		return wrap(errors.Wrapf(err, "creating %s", destPath))
	}
	defer f.Close()

	cr := &countingReader{
		r:          resp.Body,
		total:      resp.ContentLength,
		progressCb: progressCb,
	}

	if _, err := io.Copy(f, cr); err != nil {
		return wrap(errors.Wrapf(err, "writing %s", destPath))
	}

	return nil
}

func getLatestTag() (string, string, error) {
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()

	url := fmt.Sprintf("https://api.github.com/repos/%s/releases/latest", _githubRepo)
	req, err := http.NewRequestWithContext(ctx, "GET", url, nil)
	if err != nil {
		return "", "", errors.Wrapf(err, "creating request for %s", url)
	}

	client := http.DefaultClient
	resp, err := client.Do(req)
	if err != nil {
		return "", "", errors.Wrapf(err, "GET %s", url)
	}
	defer resp.Body.Close()

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", "", errors.Wrapf(err, "reading response body for %s: %#v", url, string(body))
	}

	if resp.StatusCode != http.StatusOK {
		return "", "", errors.Errorf("GET %s: HTTP %d: %#v", url, resp.StatusCode, string(body))
	}

	var release struct {
		TagName string `json:"tag_name"`
		Body    string `json:"body"`
	}
	err = json.Unmarshal(body, &release)
	if err != nil {
		return "", "", errors.Wrapf(err, "parsing JSON for %s: %#v", url, string(body))
	}

	if release.TagName == "" {
		return "", "", errors.Errorf("GET %s: tag_name is empty: %#v", url, string(body))
	}

	return release.TagName, release.Body, nil
}
