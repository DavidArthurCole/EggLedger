package platform

import (
	"path/filepath"

	"golang.org/x/sys/windows"
)

// hide hides a file or directory using SetFileAttributes.
func Hide(path string) error {
	u16ptr, err := windows.UTF16PtrFromString(path)
	if err != nil {
		return err
	}
	return windows.SetFileAttributes(u16ptr, windows.FILE_ATTRIBUTE_HIDDEN)
}

func OpenFolderAndSelect(path string) error {
	abspath, _ := filepath.Abs(path)
	verb, _ := windows.UTF16PtrFromString("open")
	lpFile, _ := windows.UTF16PtrFromString("explorer.exe")
	lpParameters, _ := windows.UTF16PtrFromString("/select," + abspath)
	return windows.ShellExecute(0, verb, lpFile, lpParameters, nil, windows.SW_SHOW)
}

// Open opens a file or URL in the default application on Windows.
func Open(target string) error {
	abspath, err := filepath.Abs(target)
	if err != nil {
		return err
	}
	verb, _ := windows.UTF16PtrFromString("open")
	lpFile, _ := windows.UTF16PtrFromString(abspath)
	return windows.ShellExecute(0, verb, lpFile, nil, nil, windows.SW_SHOW)
}
