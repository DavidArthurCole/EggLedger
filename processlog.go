package main

import (
	"context"
	"sync"
	"sync/atomic"
	"time"
)

// ProcessStatus indicates the current state of a tracked process.
type ProcessStatus string

const (
	ProcessRunning ProcessStatus = "running"
	ProcessDone    ProcessStatus = "done"
	ProcessFailed  ProcessStatus = "failed"
)

// ProcessLogEntry is a single log line within a process log.
type ProcessLogEntry struct {
	Text    string `json:"text"`
	IsError bool   `json:"isError"`
}

// SegmentStatus is a named phase within a process and its current state.
type SegmentStatus struct {
	Name   string `json:"name"`
	Status string `json:"status"` // "pending" | "active" | "done" | "failed" | "skipped"
}

// ProcessSnapshot is a JSON-serializable point-in-time copy of a Process.
type ProcessSnapshot struct {
	ID             string            `json:"id"`
	Label          string            `json:"label"`
	Status         ProcessStatus     `json:"status"`
	Logs           []ProcessLogEntry `json:"logs"`
	StartTimestamp int64             `json:"startTimestamp"` // unix millis
	Kind           string            `json:"kind"`           // "overall" | "mission"
	Segments       []SegmentStatus   `json:"segments"`
}

// Process represents a tracked unit of work with its own log stream.
type Process struct {
	id             string
	label          string
	kind           string
	status         ProcessStatus
	logs           []ProcessLogEntry
	segments       []SegmentStatus
	startTimestamp int64
	mu             sync.Mutex
	registry       *ProcessRegistry
}

// Log appends an entry to the process log and marks the registry dirty.
// It does not emit to the global app log; callers handle that separately.
func (p *Process) Log(text string, isError bool) {
	p.mu.Lock()
	p.logs = append(p.logs, ProcessLogEntry{Text: text, IsError: isError})
	p.mu.Unlock()
	p.registry.dirty.Store(true)
}

// MarkDone marks the process as successfully completed and schedules its
// removal from the registry after 5 seconds so the UI briefly shows the result.
func (p *Process) MarkDone() {
	p.mu.Lock()
	p.status = ProcessDone
	p.mu.Unlock()
	p.registry.dirty.Store(true)
	go func() {
		time.Sleep(5 * time.Second)
		p.registry.Remove(p.id)
	}()
}

// MarkFailed marks the process as failed and schedules its removal from the
// registry after 5 seconds so the UI briefly shows the failure.
func (p *Process) MarkFailed() {
	p.mu.Lock()
	p.status = ProcessFailed
	p.mu.Unlock()
	p.registry.dirty.Store(true)
	go func() {
		time.Sleep(5 * time.Second)
		p.registry.Remove(p.id)
	}()
}

// InitSegments creates segments for each name in pending state.
func (p *Process) InitSegments(names []string) {
	p.mu.Lock()
	p.segments = make([]SegmentStatus, len(names))
	for i, n := range names {
		p.segments[i] = SegmentStatus{Name: n, Status: "pending"}
	}
	p.mu.Unlock()
	p.registry.dirty.Store(true)
}

// SetSegment updates the status of the named segment.
func (p *Process) SetSegment(name, status string) {
	p.mu.Lock()
	for i := range p.segments {
		if p.segments[i].Name == name {
			p.segments[i].Status = status
			p.mu.Unlock()
			p.registry.dirty.Store(true)
			return
		}
	}
	p.mu.Unlock()
}

func (p *Process) snapshot() ProcessSnapshot {
	p.mu.Lock()
	defer p.mu.Unlock()
	logs := make([]ProcessLogEntry, len(p.logs))
	copy(logs, p.logs)
	segs := make([]SegmentStatus, len(p.segments))
	copy(segs, p.segments)
	return ProcessSnapshot{
		ID:             p.id,
		Label:          p.label,
		Status:         p.status,
		Logs:           logs,
		StartTimestamp: p.startTimestamp,
		Kind:           p.kind,
		Segments:       segs,
	}
}

// ProcessRegistry holds a set of named processes and rate-limits snapshot
// pushes to at most one every 100ms.
type ProcessRegistry struct {
	mu        sync.RWMutex
	processes map[string]*Process
	order     []string
	dirty     atomic.Bool
	flushFn   func([]ProcessSnapshot)
}

// NewProcessRegistry creates a registry that calls flushFn whenever process
// state changes, rate-limited to 100ms intervals.
func NewProcessRegistry(flushFn func([]ProcessSnapshot)) *ProcessRegistry {
	return &ProcessRegistry{
		processes: make(map[string]*Process),
		flushFn:   flushFn,
	}
}

// Register creates a new Process with the given id, label, and kind.
// If a process with the same id already exists it is replaced.
func (r *ProcessRegistry) Register(id, label, kind string) *Process {
	p := &Process{
		id:             id,
		label:          label,
		kind:           kind,
		status:         ProcessRunning,
		startTimestamp: time.Now().UnixMilli(),
		registry:       r,
	}
	r.mu.Lock()
	if _, exists := r.processes[id]; !exists {
		r.order = append(r.order, id)
	}
	r.processes[id] = p
	r.mu.Unlock()
	r.dirty.Store(true)
	return p
}

// Remove deletes a process from the registry by id.
func (r *ProcessRegistry) Remove(id string) {
	r.mu.Lock()
	delete(r.processes, id)
	for i, oid := range r.order {
		if oid == id {
			r.order = append(r.order[:i], r.order[i+1:]...)
			break
		}
	}
	r.mu.Unlock()
	r.dirty.Store(true)
}

// Snapshot returns a point-in-time copy of all processes in registration order.
func (r *ProcessRegistry) Snapshot() []ProcessSnapshot {
	r.mu.RLock()
	order := make([]string, len(r.order))
	copy(order, r.order)
	r.mu.RUnlock()
	snapshots := make([]ProcessSnapshot, 0, len(order))
	for _, id := range order {
		r.mu.RLock()
		p, ok := r.processes[id]
		r.mu.RUnlock()
		if ok {
			snapshots = append(snapshots, p.snapshot())
		}
	}
	return snapshots
}

// Start begins the rate-limited flush goroutine; it stops when ctx is cancelled.
func (r *ProcessRegistry) Start(ctx context.Context) {
	go func() {
		ticker := time.NewTicker(100 * time.Millisecond)
		defer ticker.Stop()
		for {
			select {
			case <-ctx.Done():
				return
			case <-ticker.C:
				if r.dirty.CompareAndSwap(true, false) {
					r.flushFn(r.Snapshot())
				}
			}
		}
	}()
}
