package menno

import "time"

// Host wires the main process's capabilities into the menno package, mirroring
// the dependency-injection style used by the update and reports packages. The
// main package supplies a concrete implementation via SetHost.
type Host interface {
	InternalDir() string
	ForceRefresh() bool
	AutoRefreshPref() bool
	LastRefreshAt() time.Time
	SetLastRefreshAt(t time.Time)
}

var _host Host

// SetHost wires the main process's capabilities into the menno package.
func SetHost(h Host) { _host = h }
