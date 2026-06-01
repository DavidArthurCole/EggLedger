package update

import (
	"encoding/json"
	"os"
	"path/filepath"
)

// updateStatusFileName is written next to the executable by the replace-mode
// process and read once on the next normal startup. It lives in the exe dir
// (not internal/) so both processes agree on the path with no init dependency.
const updateStatusFileName = ".egg-update-status.json"

// Status is the small payload written by the replace-mode process to report the
// outcome of an in-place update to the freshly relaunched process.
type Status struct {
	Success   bool   `json:"success"`
	Message   string `json:"message,omitempty"`
	ToVersion string `json:"toVersion,omitempty"`
	NewBinary string `json:"newBinary,omitempty"`
}

func updateStatusPath(dir string) string {
	return filepath.Join(dir, updateStatusFileName)
}

func writeUpdateStatus(dir string, s Status) error {
	data, err := json.Marshal(s)
	if err != nil {
		return err
	}
	return os.WriteFile(updateStatusPath(dir), data, 0644)
}

// ReadAndClearStatus reads and deletes the status file. Returns (nil, false)
// if absent or unreadable.
func ReadAndClearStatus(dir string) (*Status, bool) {
	path := updateStatusPath(dir)
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, false
	}
	_ = os.Remove(path)
	var s Status
	if err := json.Unmarshal(data, &s); err != nil {
		return nil, false
	}
	return &s, true
}
