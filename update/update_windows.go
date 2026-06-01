//go:build windows

package update

import (
	"time"

	"golang.org/x/sys/windows"
)

// waitForProcessExit blocks until process pid is confirmed exited or timeout
// elapses. Returns true if the process has exited. Uses WaitForSingleObject on
// a SYNCHRONIZE handle, which is authoritative (unlike merely opening a handle).
func waitForProcessExit(pid int, timeout time.Duration) bool {
	h, err := windows.OpenProcess(windows.SYNCHRONIZE, false, uint32(pid))
	if err != nil {
		// Could not open the process - treat as already gone.
		return true
	}
	defer windows.CloseHandle(h)

	ms := uint32(timeout / time.Millisecond)
	event, err := windows.WaitForSingleObject(h, ms)
	if err != nil {
		return false
	}
	return event == windows.WAIT_OBJECT_0
}
