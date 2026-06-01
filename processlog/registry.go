package processlog

import (
	"context"
	"sync"
	"sync/atomic"
	"time"
)

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
