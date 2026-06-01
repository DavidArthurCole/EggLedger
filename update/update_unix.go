//go:build !windows

package update

import (
	"syscall"
	"time"
)

// waitForProcessExit polls kill(pid, 0) until the process is gone (ESRCH) or
// the timeout elapses. Returns true if the process has exited.
func waitForProcessExit(pid int, timeout time.Duration) bool {
	deadline := time.Now().Add(timeout)
	for {
		if syscall.Kill(pid, 0) != nil {
			return true
		}
		if time.Now().After(deadline) {
			return false
		}
		time.Sleep(50 * time.Millisecond)
	}
}
