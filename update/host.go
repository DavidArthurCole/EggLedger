package update

import "time"

// Host wires the main process's capabilities into the update package, mirroring
// the dependency-injection style used by the reports package. The main package
// supplies a concrete implementation via SetHost.
type Host interface {
	Eval(js string)
	Close()
	EmitMessage(msg string, isError bool)
	AppVersion() string
	IsTranslocated() bool
	UpdateCheckSnapshot() (lastAt time.Time, knownTag, knownNotes string)
	SetUpdateCheck(tag, notes string)
}

var _host Host

// SetHost wires the main process's capabilities into the update package.
func SetHost(h Host) { _host = h }
