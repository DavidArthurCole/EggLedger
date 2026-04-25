package main

import (
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"time"
)

// runReplaceMode waits for oldPID to exit, renames self to oldPath, launches it fresh.
// Called when the binary is started with --replace-pid and --replace-path flags.
func runReplaceMode(oldPID int, oldPath string) {
	deadline := time.Now().Add(10 * time.Second)
	for time.Now().Before(deadline) {
		if !processExists(oldPID) {
			break
		}
		time.Sleep(100 * time.Millisecond)
	}
	if processExists(oldPID) {
		log.Fatalf("update: old process %d did not exit within timeout", oldPID)
	}

	self, err := os.Executable()
	if err != nil {
		log.Fatalf("update: os.Executable: %v", err)
	}

	// On Windows the old binary may remain briefly locked after process exit.
	// Retry the rename a few times before giving up.
	var renameErr error
	for i := 0; i < 5; i++ {
		renameErr = os.Rename(self, oldPath)
		if renameErr == nil {
			break
		}
		time.Sleep(500 * time.Millisecond)
	}
	if renameErr != nil {
		log.Fatalf(
			"update: could not rename %s -> %s after 5 attempts: %v\n"+
				"The updated binary is at: %s\n"+
				"You can manually replace the existing binary with it.",
			self, oldPath, renameErr, self,
		)
	}

	cmd := exec.Command(oldPath)
	if err := cmd.Start(); err != nil {
		log.Fatalf(
			"update: launch %s: %v\n"+
				"The updated binary is at: %s - you can launch it manually.",
			oldPath, err, oldPath,
		)
	}
	os.Exit(0)
}

// cleanStaleUpdateBinaries removes any leftover EggLedger_*_new[.exe] files
// in the same directory as the current executable. Called on startup.
func cleanStaleUpdateBinaries() {
	self, err := os.Executable()
	if err != nil {
		return
	}
	exeDir := filepath.Dir(self)

	patterns := []string{
		filepath.Join(exeDir, "EggLedger_*_new"),
		filepath.Join(exeDir, "EggLedger_*_new.exe"),
	}
	for _, pattern := range patterns {
		matches, err := filepath.Glob(pattern)
		if err != nil {
			continue
		}
		for _, match := range matches {
			_ = os.Remove(match)
		}
	}
}
