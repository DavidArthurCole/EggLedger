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
	if err := os.Rename(self, oldPath); err != nil {
		log.Fatalf("update: rename %s -> %s: %v", self, oldPath, err)
	}

	cmd := exec.Command(oldPath)
	if err := cmd.Start(); err != nil {
		log.Fatalf("update: launch %s: %v", oldPath, err)
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
