package main

import (
	"encoding/json"
	"io"
	"io/fs"
	"os"
	"path/filepath"
)

func copyDir(src, dst string) error {
	return filepath.WalkDir(src, func(path string, d fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		rel, err := filepath.Rel(src, path)
		if err != nil {
			return err
		}
		target := filepath.Join(dst, rel)
		if d.IsDir() {
			return os.MkdirAll(target, 0755)
		}
		return copyFile(path, target)
	})
}

func copyFile(src, dst string) (retErr error) {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()
	if err := os.MkdirAll(filepath.Dir(dst), 0755); err != nil {
		return err
	}
	out, err := os.Create(dst)
	if err != nil {
		return err
	}
	defer func() {
		if cerr := out.Close(); cerr != nil && retErr == nil {
			retErr = cerr
		}
	}()
	_, retErr = io.Copy(out, in)
	return retErr
}

type bootstrapConfig struct {
	DataRootDir string `json:"data_root_dir,omitempty"`
	InternalDir string `json:"internal_dir,omitempty"` // legacy
}

func bootstrapPath() string {
	configDir, err := os.UserConfigDir()
	if err != nil {
		return ""
	}
	return filepath.Join(configDir, "EggLedger", "bootstrap.json")
}

func readBootstrapConfig() bootstrapConfig {
	p := bootstrapPath()
	if p == "" {
		return bootstrapConfig{}
	}
	data, err := os.ReadFile(p)
	if err != nil {
		return bootstrapConfig{}
	}
	var cfg bootstrapConfig
	_ = json.Unmarshal(data, &cfg)
	return cfg
}

// writeBootstrapConfig stores dataRootDir as the canonical root for all app
// data (internal/, exports/, logs/). This replaces the legacy internal_dir key.
func writeBootstrapConfig(dataRootDir string) error {
	p := bootstrapPath()
	if p == "" {
		return os.ErrNotExist
	}
	if err := os.MkdirAll(filepath.Dir(p), 0755); err != nil {
		return err
	}
	data, err := json.Marshal(bootstrapConfig{DataRootDir: dataRootDir})
	if err != nil {
		return err
	}
	return os.WriteFile(p, data, 0644)
}

// resolveInternalDir returns the directory used for the app's internal data
// (SQLite DBs, caches). Checks bootstrap.json first, falls back to rootDir/internal.
func resolveInternalDir(rootDir string) string {
	cfg := readBootstrapConfig()
	if cfg.DataRootDir != "" {
		return filepath.Join(cfg.DataRootDir, "internal")
	}
	if cfg.InternalDir != "" {
		return cfg.InternalDir
	}
	return filepath.Join(rootDir, "internal")
}

// resolveExportsDir returns the directory used for CSV/XLSX exports.
func resolveExportsDir(rootDir string) string {
	cfg := readBootstrapConfig()
	if cfg.DataRootDir != "" {
		return filepath.Join(cfg.DataRootDir, "exports")
	}
	return filepath.Join(rootDir, "exports")
}

// resolveLogsDir returns the directory used for log files.
func resolveLogsDir(rootDir string) string {
	cfg := readBootstrapConfig()
	if cfg.DataRootDir != "" {
		return filepath.Join(cfg.DataRootDir, "logs")
	}
	return filepath.Join(rootDir, "logs")
}

// resolveDataRootDir returns the root directory that owns all app data.
// When a data_root_dir is set in bootstrap.json, that is the root.
// For legacy internal_dir, we fall back to rootDir (exe directory).
func resolveDataRootDir(rootDir string) string {
	cfg := readBootstrapConfig()
	if cfg.DataRootDir != "" {
		return cfg.DataRootDir
	}
	return rootDir
}
