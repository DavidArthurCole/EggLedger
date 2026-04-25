package platform

import (
	"os/exec"
	"path/filepath"
	"strings"

	"golang.org/x/sys/windows"
)

// Hide hides a file or directory using SetFileAttributes.
func Hide(path string) error {
	u16ptr, err := windows.UTF16PtrFromString(path)
	if err != nil {
		return err
	}
	return windows.SetFileAttributes(u16ptr, windows.FILE_ATTRIBUTE_HIDDEN)
}

// Show removes the hidden attribute from a file or directory.
func Show(path string) error {
	p, err := windows.UTF16PtrFromString(path)
	if err != nil {
		return err
	}
	attrs, err := windows.GetFileAttributes(p)
	if err != nil {
		return err
	}
	return windows.SetFileAttributes(p, attrs&^windows.FILE_ATTRIBUTE_HIDDEN)
}

func OpenFolderAndSelect(path string) error {
	abspath, _ := filepath.Abs(path)
	verb, _ := windows.UTF16PtrFromString("open")
	lpFile, _ := windows.UTF16PtrFromString("explorer.exe")
	lpParameters, _ := windows.UTF16PtrFromString("/select," + abspath)
	return windows.ShellExecute(0, verb, lpFile, lpParameters, nil, windows.SW_SHOW)
}

// ChooseFolder opens a native folder picker dialog via PowerShell.
// Returns the selected path, or "" if cancelled.
func ChooseFolder() string {
	script := `Add-Type -AssemblyName System.Windows.Forms; $f=New-Object System.Windows.Forms.FolderBrowserDialog; $f.ShowNewFolderButton=$true; if($f.ShowDialog() -eq 'OK'){Write-Output $f.SelectedPath}`
	cmd := exec.Command("powershell", "-NoProfile", "-WindowStyle", "Hidden", "-Command", script)
	out, err := cmd.Output()
	if err != nil {
		return ""
	}
	return strings.TrimSpace(string(out))
}

// Open opens a file or URL in the default application on Windows.
func Open(target string) error {
	verb, _ := windows.UTF16PtrFromString("open")
	var lpFile *uint16
	if len(target) > 0 && (len(target) >= 7 && (target[:7] == "http://" || (len(target) >= 8 && target[:8] == "https://"))) {
		lpFile, _ = windows.UTF16PtrFromString(target)
	} else {
		abspath, err := filepath.Abs(target)
		if err != nil {
			return err
		}
		lpFile, _ = windows.UTF16PtrFromString(abspath)
	}
	return windows.ShellExecute(0, verb, lpFile, nil, nil, windows.SW_SHOW)
}
