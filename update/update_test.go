package update

import (
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"testing"
	"time"
)

func TestWaitForProcessExit(t *testing.T) {
	// A process that exits quickly.
	var sleeper *exec.Cmd
	if runtime.GOOS == "windows" {
		sleeper = exec.Command("cmd", "/c", "ping", "127.0.0.1", "-n", "2")
	} else {
		sleeper = exec.Command("sleep", "0.3")
	}
	if err := sleeper.Start(); err != nil {
		t.Fatalf("start sleeper: %v", err)
	}
	pid := sleeper.Process.Pid

	// While alive, a 50ms wait should time out (return false).
	if waitForProcessExit(pid, 50*time.Millisecond) {
		t.Fatalf("expected process %d still alive at 50ms", pid)
	}

	// Let it finish; reap it so the OS releases the PID.
	_ = sleeper.Wait()

	// A dead process should be reported exited promptly.
	if !waitForProcessExit(pid, 2*time.Second) {
		t.Fatalf("expected process %d to be reported exited", pid)
	}
}

func TestUpdateStatusRoundTrip(t *testing.T) {
	dir := t.TempDir()

	// No file -> read returns (nil, false).
	if s, ok := ReadAndClearStatus(dir); ok || s != nil {
		t.Fatalf("expected no status, got %v %v", s, ok)
	}

	if err := writeUpdateStatus(dir, Status{Success: true, ToVersion: "2.2.0"}); err != nil {
		t.Fatalf("write: %v", err)
	}

	s, ok := ReadAndClearStatus(dir)
	if !ok || s == nil {
		t.Fatalf("expected status present")
	}
	if !s.Success || s.ToVersion != "2.2.0" {
		t.Fatalf("unexpected status: %+v", s)
	}

	// File must be cleared after read.
	if _, ok := ReadAndClearStatus(dir); ok {
		t.Fatalf("expected status cleared after read")
	}
}

func TestAcquireUpdateLock(t *testing.T) {
	dir := t.TempDir()
	lock := filepath.Join(dir, ".egg-update.lock")

	release, ok := acquireUpdateLock(lock)
	if !ok {
		t.Fatalf("first acquire should succeed")
	}

	// Second acquire while held (live PID inside) must fail.
	if _, ok2 := acquireUpdateLock(lock); ok2 {
		t.Fatalf("second acquire should fail while lock held")
	}

	release()

	// After release the lock file is gone and acquire succeeds again.
	release2, ok3 := acquireUpdateLock(lock)
	if !ok3 {
		t.Fatalf("acquire after release should succeed")
	}
	release2()
}

func TestIsNewBinaryName(t *testing.T) {
	cases := []struct {
		path string
		want bool
	}{
		{"EggLedger_new.exe", true},
		{"EggLedger_new", true},
		{"EggLedger.exe", false},
		{"EggLedger", false},
		{filepath.Join("C:\\x", "EggLedger_new.exe"), true},
		{filepath.Join("C:\\x", "EggLedger.exe"), false},
	}
	for _, c := range cases {
		if got := isNewBinaryName(c.path); got != c.want {
			t.Errorf("isNewBinaryName(%q) = %v, want %v", c.path, got, c.want)
		}
	}
}

func TestCanonicalPathFromNew(t *testing.T) {
	dir := t.TempDir()
	cases := []struct {
		in   string
		want string
	}{
		{filepath.Join(dir, "EggLedger_new.exe"), filepath.Join(dir, "EggLedger.exe")},
		{filepath.Join(dir, "EggLedger_new"), filepath.Join(dir, "EggLedger")},
	}
	for _, c := range cases {
		if got := canonicalPathFromNew(c.in); got != c.want {
			t.Errorf("canonicalPathFromNew(%q) = %q, want %q", c.in, got, c.want)
		}
	}
}

func TestDecideReplace(t *testing.T) {
	dir := t.TempDir()
	newSelf := filepath.Join(dir, "EggLedger_new.exe")
	normalSelf := filepath.Join(dir, "EggLedger.exe")
	canonical := filepath.Join(dir, "EggLedger.exe")
	flagPath := filepath.Join(dir, "Installed", "EggLedger.exe")

	cases := []struct {
		name        string
		self        string
		replacePID  int
		replacePath string
		wantRun     bool
		wantPID     int
		wantPath    string
	}{
		{
			name:    "normal name no flags",
			self:    normalSelf,
			wantRun: false,
		},
		{
			name:     "new name no flags",
			self:     newSelf,
			wantRun:  true,
			wantPID:  0,
			wantPath: canonical,
		},
		{
			name:        "new name with flags",
			self:        newSelf,
			replacePID:  4242,
			replacePath: flagPath,
			wantRun:     true,
			wantPID:     4242,
			wantPath:    flagPath,
		},
		{
			name:        "normal name with legacy flags",
			self:        normalSelf,
			replacePID:  9999,
			replacePath: flagPath,
			wantRun:     true,
			wantPID:     9999,
			wantPath:    flagPath,
		},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			run, pid, path := decideReplace(c.self, c.replacePID, c.replacePath)
			if run != c.wantRun {
				t.Fatalf("run = %v, want %v", run, c.wantRun)
			}
			if !run {
				return
			}
			if pid != c.wantPID {
				t.Errorf("pid = %d, want %d", pid, c.wantPID)
			}
			if path != c.wantPath {
				t.Errorf("path = %q, want %q", path, c.wantPath)
			}
		})
	}
}

func TestHandshakeReadyPing(t *testing.T) {
	token := "tok123"
	addr, closer, err := startHandshakeListener(token)
	if err != nil {
		t.Fatalf("listener: %v", err)
	}
	defer closer.Close()

	// Wrong token must NOT signal.
	pingOldReady(addr, "wrong")
	select {
	case <-_updateHandoff:
		t.Fatalf("handoff signaled with wrong token")
	case <-time.After(200 * time.Millisecond):
	}

	// Correct token signals exactly once.
	pingOldReady(addr, token)
	select {
	case <-_updateHandoff:
	case <-time.After(2 * time.Second):
		t.Fatalf("expected handoff signal")
	}
}

func TestRenameWithRetry(t *testing.T) {
	dir := t.TempDir()
	src := filepath.Join(dir, "src.bin")
	dst := filepath.Join(dir, "dst.bin")
	if err := os.WriteFile(src, []byte("new"), 0755); err != nil {
		t.Fatalf("write src: %v", err)
	}
	if err := renameWithRetry(src, dst, 3, 10*time.Millisecond); err != nil {
		t.Fatalf("rename: %v", err)
	}
	data, err := os.ReadFile(dst)
	if err != nil || string(data) != "new" {
		t.Fatalf("dst content: %q err %v", data, err)
	}
}
