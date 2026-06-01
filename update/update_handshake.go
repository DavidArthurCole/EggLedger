package update

import (
	"crypto/rand"
	"encoding/hex"
	"net"
	"net/http"
	"sync/atomic"
)

// _updateHandoff is signaled when the freshly launched new instance reports it
// is fully up. The main loop reacts by stopping the www server, showing a
// countdown, and exiting.
var _updateHandoff = make(chan struct{}, 1)

// _handoffDone is set to 1 by the /ready handler once a valid ping arrives. The
// abort-timeout goroutine checks this flag rather than draining _updateHandoff,
// so it never steals the signal from main's select.
var _handoffDone int32

// _handshakeServed is closed exactly once when a valid /ready ping arrives, so
// the abort goroutine can stop waiting and close the listener promptly on the
// happy path instead of always sleeping out its timeout.
var _handshakeServed = make(chan struct{})

// HandshakeServed returns a channel closed when the new instance has reported in.
func HandshakeServed() <-chan struct{} {
	return _handshakeServed
}

// HandoffChan returns the receive end of the internal handoff channel so the
// main process can select on it.
func HandoffChan() <-chan struct{} {
	return _updateHandoff
}

// newHandshakeToken returns a random hex token used to authenticate the ping.
func newHandshakeToken() string {
	b := make([]byte, 16)
	if _, err := rand.Read(b); err != nil {
		return "egg-update"
	}
	return hex.EncodeToString(b)
}

// startHandshakeListener opens a localhost-only HTTP listener. A POST to
// /ready carrying the matching token signals _updateHandoff exactly once.
// Returns the listener address ("127.0.0.1:N") so it can be passed to the new
// process. The caller owns lifecycle via the returned io.Closer.
func startHandshakeListener(token string) (addr string, closer interface{ Close() error }, err error) {
	ln, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		return "", nil, err
	}
	mux := http.NewServeMux()
	mux.HandleFunc("/ready", func(w http.ResponseWriter, r *http.Request) {
		if r.URL.Query().Get("token") != token {
			http.Error(w, "bad token", http.StatusForbidden)
			return
		}
		w.WriteHeader(http.StatusOK)
		// CompareAndSwap gates the one-shot signal so concurrent /ready requests
		// race-free (no shared bool), and closes _handshakeServed exactly once.
		if atomic.CompareAndSwapInt32(&_handoffDone, 0, 1) {
			close(_handshakeServed)
			select {
			case _updateHandoff <- struct{}{}:
			default:
			}
		}
	})
	srv := &http.Server{Handler: mux}
	go func() { _ = srv.Serve(ln) }()
	return ln.Addr().String(), srv, nil
}
