package update

import (
	"net/http"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

const updateLockFileName = ".egg-update.lock"

// newBinarySuffix marks a freshly downloaded binary awaiting self-replacement.
const newBinarySuffix = "_new"

// isNewBinaryName reports whether path's base name (minus .exe) ends in _new,
// i.e. it is a downloaded update binary that should replace EggLedger[.exe].
func isNewBinaryName(path string) bool {
	base := filepath.Base(path)
	base = strings.TrimSuffix(base, ".exe")
	return strings.HasSuffix(base, newBinarySuffix)
}

// canonicalPathFromNew maps <dir>/EggLedger_new[.exe] -> <dir>/EggLedger[.exe].
func canonicalPathFromNew(path string) string {
	dir := filepath.Dir(path)
	base := filepath.Base(path)
	ext := ""
	if strings.HasSuffix(base, ".exe") {
		ext = ".exe"
		base = strings.TrimSuffix(base, ".exe")
	}
	base = strings.TrimSuffix(base, newBinarySuffix)
	return filepath.Join(dir, base+ext)
}

// decideReplace decides whether this process should run as the updater that
// replaces EggLedger[.exe]. Trigger if the running binary is named *_new, OR
// legacy --replace-* flags are present. oldPath comes from replacePath when
// given, else derived from the _new name. oldPID is 0 when unknown (name-based
// launch with no flags).
func decideReplace(self string, replacePID int, replacePath string) (run bool, oldPID int, oldPath string) {
	hasFlags := replacePID != 0 && replacePath != ""
	isNew := self != "" && isNewBinaryName(self)
	if !isNew && !hasFlags {
		return false, 0, ""
	}
	oldPath = replacePath
	if oldPath == "" {
		oldPath = canonicalPathFromNew(self)
	}
	return true, replacePID, oldPath
}

// ResolveReplaceMode wraps decideReplace with the running executable path,
// reading the package's own replace flags.
func ResolveReplaceMode() (run bool, oldPID int, oldPath string) {
	self, err := os.Executable()
	if err != nil {
		self = ""
	}
	return decideReplace(self, _replacePID, _replacePath)
}

// acquireUpdateLock creates lockPath exclusively, writing the current PID.
// Returns a release func and true on success. If the lock already exists and
// the PID inside is still alive, returns (nil, false). A stale lock (dead PID)
// is reclaimed.
func acquireUpdateLock(lockPath string) (func(), bool) {
	tryCreate := func() (*os.File, error) {
		return os.OpenFile(lockPath, os.O_CREATE|os.O_EXCL|os.O_WRONLY, 0644)
	}

	f, err := tryCreate()
	if err != nil {
		// Lock exists - check whether its owner is still alive.
		data, rerr := os.ReadFile(lockPath)
		if rerr == nil {
			if pid, perr := strconv.Atoi(strings.TrimSpace(string(data))); perr == nil {
				if !waitForProcessExit(pid, 0) {
					// Owner still alive - cannot acquire.
					return nil, false
				}
			}
		}
		// Stale or unreadable lock - reclaim it.
		_ = os.Remove(lockPath)
		f, err = tryCreate()
		if err != nil {
			return nil, false
		}
	}

	_, _ = f.WriteString(strconv.Itoa(os.Getpid()))
	_ = f.Close()

	released := false
	release := func() {
		if released {
			return
		}
		released = true
		_ = os.Remove(lockPath)
	}
	return release, true
}

// renameWithRetry renames src to dst, retrying on failure to tolerate brief
// Windows file locks (antivirus, indexer) after a process exits.
func renameWithRetry(src, dst string, attempts int, delay time.Duration) error {
	var err error
	for i := 0; i < attempts; i++ {
		err = os.Rename(src, dst)
		if err == nil {
			return nil
		}
		time.Sleep(delay)
	}
	return err
}

// pingOldReady tells the old instance (listening on addr) that the new instance
// is fully up. Best-effort with a few quick retries.
func pingOldReady(addr, token string) {
	url := "http://" + addr + "/ready?token=" + token
	client := &http.Client{Timeout: 2 * time.Second}
	for i := 0; i < 5; i++ {
		resp, err := client.Post(url, "text/plain", nil)
		if err == nil {
			_ = resp.Body.Close()
			if resp.StatusCode == http.StatusOK {
				return
			}
		}
		time.Sleep(300 * time.Millisecond)
	}
}

// RunTakeover runs in the new (full-UI) instance after startup. It pings the old
// instance, waits for it to exit, then renames its own running image to oldPath
// so the directory keeps a single canonical EggLedger[.exe]. It stays running as
// the live app (no relaunch). Handshake parameters come from the package's own
// flags.
func RunTakeover(oldPID int, oldPath string) {
	runTakeover(oldPID, oldPath, _handshakePort, _handshakeToken)
}

// runTakeover is the internal implementation taking explicit handshake params so
// it can be exercised in tests.
func runTakeover(oldPID int, oldPath, hsAddr, hsToken string) {
	if hsAddr != "" && hsToken != "" {
		pingOldReady(hsAddr, hsToken)
	}

	if oldPID > 0 {
		waitForProcessExit(oldPID, 30*time.Second)
	}

	self, err := os.Executable()
	if err != nil {
		return
	}
	exeDir := filepath.Dir(oldPath)
	release, ok := acquireUpdateLock(filepath.Join(exeDir, updateLockFileName))
	if !ok {
		return
	}
	defer release()

	if sameFile(self, oldPath) {
		_host.EmitMessage("Updated to v"+appVersionString()+".", false)
		return
	}

	attempts, delay := 10, 300*time.Millisecond
	if oldPID == 0 {
		attempts, delay = 60, 500*time.Millisecond
	}
	if err := renameWithRetry(self, oldPath, attempts, delay); err != nil {
		_host.EmitMessage("Update applied, but the binary could not be renamed to "+filepath.Base(oldPath)+". The running app is up to date.", true)
		return
	}
	_host.EmitMessage("Updated to v"+appVersionString()+".", false)
}

// sameFile reports whether a and b resolve to the same on-disk file.
func sameFile(a, b string) bool {
	ai, err1 := os.Stat(a)
	bi, err2 := os.Stat(b)
	if err1 != nil || err2 != nil {
		ra, _ := filepath.Abs(a)
		rb, _ := filepath.Abs(b)
		return ra == rb
	}
	return os.SameFile(ai, bi)
}

// appVersionString returns the running app version (embedded from VERSION) for
// update toasts.
func appVersionString() string {
	return strings.TrimSpace(_host.AppVersion())
}

// CleanStaleBinaries removes leftover EggLedger*_new[.exe] files and a
// stale .egg-update.lock (dead owner) in the executable's directory. Runs on
// startup.
func CleanStaleBinaries() {
	self, err := os.Executable()
	if err != nil {
		return
	}
	exeDir := filepath.Dir(self)

	patterns := []string{
		filepath.Join(exeDir, "EggLedger*_new"),
		filepath.Join(exeDir, "EggLedger*_new.exe"),
	}
	for _, pattern := range patterns {
		matches, err := filepath.Glob(pattern)
		if err != nil {
			continue
		}
		for _, match := range matches {
			// Never delete our own running image: a freshly launched
			// EggLedger_new instance matches this glob and still needs to
			// rename itself into place during takeover.
			if sameFile(match, self) {
				continue
			}
			_ = os.Remove(match)
		}
	}

	// Remove a stale lock whose owner is dead.
	lockPath := filepath.Join(exeDir, updateLockFileName)
	if data, err := os.ReadFile(lockPath); err == nil {
		if pid, perr := strconv.Atoi(strings.TrimSpace(string(data))); perr == nil {
			if waitForProcessExit(pid, 0) {
				_ = os.Remove(lockPath)
			}
		} else {
			_ = os.Remove(lockPath)
		}
	}
}
