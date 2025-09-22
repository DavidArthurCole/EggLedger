//go:build !(darwin || windows)

package platform

import (
	"path/filepath"

	"github.com/skratchdot/open-golang/open"
)

// Hide is a noop on Linux. I don't think there's a unified way to hide files
// or directories on Linux (what does that even mean?) other than using a dot.
func Hide(path string) error {
	return nil
}

// OpenFolderAndSelect opens the folder, since file selection depends on the
// file explorer and can't be implemented in the general case.
func OpenFolderAndSelect(path string) error {
	return open.Start(filepath.Dir(path))
}
