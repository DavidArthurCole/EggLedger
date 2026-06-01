package update

// updater_bindings.go - In-place updater implementation.

import (
	"archive/tar"
	"archive/zip"
	"compress/gzip"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"sync/atomic"
	"time"

	"github.com/pkg/errors"
)

func init() {
	go CleanStaleBinaries()
}

// HandleDownloadAndInstall is the implementation called by the MustBind
// handler registered in main.go under "downloadAndInstallUpdate".
func HandleDownloadAndInstall(tag string) error {
	action := "downloadAndInstallUpdate"
	wrap := func(err error) error { return errors.Wrap(err, action) }

	if _host.IsTranslocated() {
		return wrap(errors.New("cannot update while app is translocated: move EggLedger out of Downloads first"))
	}

	exePath, err := os.Executable()
	if err != nil {
		return wrap(err)
	}
	exeDir := filepath.Dir(exePath)

	assetURL, err := getUpdateAssetURL(tag)
	if err != nil {
		return wrap(err)
	}

	exeName := filepath.Base(exePath)
	tempBinName := strings.TrimSuffix(exeName, ".exe") + "_new"
	if runtime.GOOS == "windows" {
		tempBinName += ".exe"
	}
	tempPath := filepath.Join(exeDir, tempBinName)

	// Clean up any previous failed attempt.
	_ = os.Remove(tempPath)

	progressCb := func(downloaded, total int64) {
		go _host.Eval(fmt.Sprintf(`globalThis.updateDownloadProgress && globalThis.updateDownloadProgress(%d, %d)`, downloaded, total))
	}

	if runtime.GOOS == "windows" {
		// Windows asset is a raw binary; download directly to tempPath.
		if err := downloadUpdate(assetURL, tempPath, progressCb); err != nil {
			_ = os.Remove(tempPath)
			return wrap(err)
		}
	} else {
		// Non-Windows assets are archives; download then extract the binary.
		archivePath := filepath.Join(os.TempDir(), expectedAssetName())
		_ = os.Remove(archivePath)

		if err := downloadUpdate(assetURL, archivePath, progressCb); err != nil {
			_ = os.Remove(archivePath)
			return wrap(err)
		}
		if err := extractBinaryFromArchive(archivePath, tempPath); err != nil {
			_ = os.Remove(archivePath)
			_ = os.Remove(tempPath)
			return wrap(err)
		}
		_ = os.Remove(archivePath)

		if err := os.Chmod(tempPath, 0755); err != nil {
			_ = os.Remove(tempPath)
			return wrap(err)
		}
	}

	currentPID := os.Getpid()
	token := newHandshakeToken()
	hsAddr, hsCloser, hsErr := startHandshakeListener(token)
	args := []string{
		fmt.Sprintf("--replace-pid=%d", currentPID),
		fmt.Sprintf("--replace-path=%s", exePath),
	}
	if hsErr == nil {
		args = append(args, "--handshake-port="+hsAddr, "--handshake-token="+token)
	}
	cmd := exec.Command(tempPath, args...)
	if err := cmd.Start(); err != nil {
		if hsCloser != nil {
			_ = hsCloser.Close()
		}
		_ = os.Remove(tempPath)
		return wrap(err)
	}

	// Switch the UI to the updating screen and stay up until the new instance
	// pings us (handled in main's select via _updateHandoff). Do NOT close here.
	go _host.Eval(`globalThis.beginUpdate && globalThis.beginUpdate()`)

	// Wait for the new instance to report in, or abort if it never does. Either
	// way, release the handshake listener afterward so it does not linger.
	go func() {
		select {
		case <-_handshakeServed:
			// New instance is up; just release the listener below.
		case <-time.After(90 * time.Second):
			if atomic.LoadInt32(&_handoffDone) == 0 {
				go _host.Eval(`globalThis.endUpdate && globalThis.endUpdate()`)
				_host.EmitMessage("Update did not start. The downloaded update is at "+tempPath+".", true)
			}
		}
		if hsCloser != nil {
			_ = hsCloser.Close()
		}
	}()

	return nil
}

// extractBinaryFromArchive dispatches to the correct extractor based on archive extension.
func extractBinaryFromArchive(archivePath, destPath string) error {
	wrap := func(err error) error { return errors.Wrap(err, "extractBinaryFromArchive") }
	switch {
	case strings.HasSuffix(archivePath, ".tar.gz"):
		return wrap(extractFromTarGz(archivePath, destPath))
	case strings.HasSuffix(archivePath, ".zip"):
		return wrap(extractFromZip(archivePath, destPath))
	default:
		return wrap(errors.Errorf("unsupported archive format: %s", filepath.Base(archivePath)))
	}
}

// extractFromTarGz extracts the first regular file from a .tar.gz archive.
func extractFromTarGz(archivePath, destPath string) error {
	f, err := os.Open(archivePath)
	if err != nil {
		return err
	}
	defer f.Close()

	gr, err := gzip.NewReader(f)
	if err != nil {
		return err
	}
	defer gr.Close()

	tr := tar.NewReader(gr)
	for {
		hdr, err := tr.Next()
		if err == io.EOF {
			break
		}
		if err != nil {
			return err
		}
		if hdr.Typeflag != tar.TypeReg || hdr.Size == 0 {
			continue
		}
		out, err := os.Create(destPath)
		if err != nil {
			return err
		}
		if _, err := io.Copy(out, tr); err != nil {
			out.Close()
			return err
		}
		return out.Close()
	}
	return errors.New("no regular file found in archive")
}

// extractFromZip extracts the EggLedger binary from a .zip archive.
// Prefers an entry under a MacOS/ path (macOS app bundle layout), then
// falls back to the first extensionless regular file.
func extractFromZip(archivePath, destPath string) error {
	zr, err := zip.OpenReader(archivePath)
	if err != nil {
		return err
	}
	defer zr.Close()

	var target *zip.File
	for _, f := range zr.File {
		if f.FileInfo().IsDir() {
			continue
		}
		if strings.Contains(f.Name, "/MacOS/") {
			target = f
			break
		}
	}
	if target == nil {
		for _, f := range zr.File {
			if f.FileInfo().IsDir() {
				continue
			}
			if filepath.Ext(f.Name) == "" {
				target = f
				break
			}
		}
	}
	if target == nil {
		return errors.New("no suitable binary found in zip archive")
	}

	rc, err := target.Open()
	if err != nil {
		return err
	}
	defer rc.Close()

	out, err := os.Create(destPath)
	if err != nil {
		return err
	}
	if _, err := io.Copy(out, rc); err != nil {
		out.Close()
		return err
	}
	return out.Close()
}
