package platform

import (
	"os/exec"
	"path/filepath"
	"strings"

	"golang.org/x/sys/unix"
)

// For some reason UF_HIDDEN isn't defined in the syscall package. The value is
// thus copied from $(xcrun --show-sdk-path)/usr/include/sys/stat.h.
const UF_HIDDEN = 0x00008000

// Hide hides a file or directory using chflags(2).
func Hide(path string) error {
	return unix.Chflags(path, UF_HIDDEN)
}

// Show removes the hidden flag from a file or directory using chflags(2).
func Show(path string) error {
	var stat unix.Stat_t
	if err := unix.Lstat(path, &stat); err != nil {
		return err
	}
	return unix.Chflags(path, int(stat.Flags)&^UF_HIDDEN)
}

// The following is a failed attempt using cgo and obj-c to implement
// openFolderAndSelect with activateFileViewerSelectingURLs. For some reason, it
// works without Lorca, but when Lorca is used, Finder becomes unresponsive until
// the Lorca app quits. Not sure which thread is blocked and how to unblock it.

// /*
// #cgo CFLAGS: -x objective-c
// #cgo LDFLAGS: -framework Cocoa -framework Foundation
// #import <Cocoa/Cocoa.h>
// void selectFile(const char *path) {
//   NSArray *files =
//       @[ [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]] ];
//   [[NSWorkspace sharedWorkspace] activateFileViewerSelectingURLs:files];
//   return;
// }
// */
// import "C"
//
// func openFolderAndSelect(path string) {
//	// Convert path to absolute first.
// 	C.selectFile(C.CString(path))
// }

// OpenFolderAndSelect selects the file in Finder using AppleScript.
// This will lead to a permission prompt on first use.
func OpenFolderAndSelect(path string) error {
	abspath, err := filepath.Abs(path)
	if err != nil {
		return err
	}
	cmd := exec.Command("osascript", "-e",
		`tell application "Finder" to activate
		tell application "Finder" to select file POSIX file `+quoteStringForAppleScript(abspath))
	return cmd.Run()
}

// Open opens a file or URL using the default application on macOS.
func Open(target string) error {
	return exec.Command("open", target).Start()
}

// ChooseFolder opens a native folder picker dialog via AppleScript.
// Returns the selected path, or "" if cancelled.
func ChooseFolder() string {
	cmd := exec.Command("osascript", "-e", `POSIX path of (choose folder)`)
	out, err := cmd.Output()
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(out))
}

// ChooseSaveFilePath opens a native save-file dialog via AppleScript.
// defaultName is the suggested filename. Returns the chosen path, or "" if cancelled.
func ChooseSaveFilePath(defaultName string) string {
	script := `POSIX path of (choose file name with prompt "Save as:" default name "` + defaultName + `")`
	cmd := exec.Command("osascript", "-e", script)
	out, err := cmd.Output()
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(out))
}

// quoteStringForAppleScript quotes backslashes and double quotes.
// See "Special String Characters" in
// https://developer.apple.com/library/archive/documentation/AppleScript/Conceptual/AppleScriptLangGuide/reference/ASLR_classes.html
func quoteStringForAppleScript(s string) string {
	s = strings.ReplaceAll(s, "\\", "\\\\")
	s = strings.ReplaceAll(s, "\"", "\\\"")
	return `"` + s + `"`
}
