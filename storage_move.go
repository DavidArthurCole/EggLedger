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

func writeBootstrapConfig(newInternalDir string) error {
	configDir, err := os.UserConfigDir()
	if err != nil {
		return err
	}
	dir := filepath.Join(configDir, "EggLedger")
	if err := os.MkdirAll(dir, 0755); err != nil {
		return err
	}
	data, err := json.Marshal(map[string]string{"internal_dir": newInternalDir})
	if err != nil {
		return err
	}
	return os.WriteFile(filepath.Join(dir, "bootstrap.json"), data, 0644)
}

func resolveInternalDir(rootDir string) string {
	configDir, err := os.UserConfigDir()
	if err == nil {
		bootstrap := filepath.Join(configDir, "EggLedger", "bootstrap.json")
		data, readErr := os.ReadFile(bootstrap)
		if readErr == nil {
			var cfg struct {
				InternalDir string `json:"internal_dir"`
			}
			if json.Unmarshal(data, &cfg) == nil && cfg.InternalDir != "" {
				return cfg.InternalDir
			}
		}
	}
	return filepath.Join(rootDir, "internal")
}
