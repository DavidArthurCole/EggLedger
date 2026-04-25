package main

// updater_bindings.go - In-place updater implementation.
//
// INTEGRATION NOTE FOR main.go:
// After `ui := UI{u}` in main(), add the following two lines:
//
//   _ui = ui
//   ui.MustBind("downloadAndInstallUpdate", handleDownloadAndInstallUpdate)
//
// Also, after flag.Parse(), add replace-mode handling:
//
//   if _replacePID != 0 && _replacePath != "" {
//     runReplaceMode(_replacePID, _replacePath)
//   }

import (
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"

	"github.com/pkg/errors"
)

// _ui holds a reference to the UI so the updater can push progress callbacks
// and close the window after spawning the new binary.
// Assigned by main() immediately after the UI is created:
//
//	_ui = ui
var _ui UI

func init() {
	go cleanStaleUpdateBinaries()
}

// handleDownloadAndInstallUpdate is the implementation called by the MustBind
// handler registered in main.go under "downloadAndInstallUpdate".
func handleDownloadAndInstallUpdate(tag string) error {
	action := "downloadAndInstallUpdate"
	wrap := func(err error) error { return errors.Wrap(err, action) }

	if _appIsTranslocated {
		return wrap(errors.New("cannot update while app is translocated: move EggLedger out of Downloads first"))
	}

	exePath, err := os.Executable()
	if err != nil {
		return wrap(err)
	}
	exeDir := filepath.Dir(exePath)

	assetURL, err := getUpdateAssetURL(tag)
	if err != nil {
		return wrap(err)
	}

	assetName := expectedAssetName()
	tempName := strings.TrimSuffix(assetName, ".exe") + "_new"
	if runtime.GOOS == "windows" {
		tempName += ".exe"
	}
	tempPath := filepath.Join(exeDir, tempName)

	// Clean up any previous failed attempt.
	_ = os.Remove(tempPath)

	progressCb := func(downloaded, total int64) {
		go _ui.Eval(fmt.Sprintf(`globalThis.updateDownloadProgress && globalThis.updateDownloadProgress(%d, %d)`, downloaded, total))
	}

	if err := downloadUpdate(assetURL, tempPath, progressCb); err != nil {
		_ = os.Remove(tempPath)
		return wrap(err)
	}

	// Make executable on Unix.
	if runtime.GOOS != "windows" {
		if err := os.Chmod(tempPath, 0755); err != nil {
			_ = os.Remove(tempPath)
			return wrap(err)
		}
	}

	currentPID := os.Getpid()
	cmd := exec.Command(
		tempPath,
		fmt.Sprintf("--replace-pid=%d", currentPID),
		fmt.Sprintf("--replace-path=%s", exePath),
	)
	if err := cmd.Start(); err != nil {
		_ = os.Remove(tempPath)
		return wrap(err)
	}

	// Close the UI - the new binary takes over.
	_ui.Close()
	return nil
}
