package main

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestSubmitEidToMenno_Success(t *testing.T) {
	var gotEID string
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			t.Errorf("expected POST, got %s", r.Method)
		}
		var body struct {
			EID string `json:"eid"`
		}
		if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
			t.Errorf("decode body: %v", err)
		}
		gotEID = body.EID
		w.WriteHeader(http.StatusOK)
	}))
	defer srv.Close()

	orig := _mennoProxyURL
	_mennoProxyURL = srv.URL
	defer func() { _mennoProxyURL = orig }()

	if err := submitEidToMenno(context.Background(), "EI1234567890"); err != nil {
		t.Errorf("expected nil error, got %v", err)
	}
	if gotEID != "EI1234567890" {
		t.Errorf("expected EID 'EI1234567890', got %q", gotEID)
	}
}

func TestSubmitEidToMenno_NonOKStatus(t *testing.T) {
	srv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusInternalServerError)
	}))
	defer srv.Close()

	orig := _mennoProxyURL
	_mennoProxyURL = srv.URL
	defer func() { _mennoProxyURL = orig }()

	if err := submitEidToMenno(context.Background(), "EI1234567890"); err == nil {
		t.Error("expected error for non-200 response, got nil")
	}
}

func TestSubmitEidToMenno_CancelledContext(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	cancel()

	orig := _mennoProxyURL
	_mennoProxyURL = "http://127.0.0.1:1" // unreachable
	defer func() { _mennoProxyURL = orig }()

	if err := submitEidToMenno(ctx, "EI1234567890"); err == nil {
		t.Error("expected error for cancelled context, got nil")
	}
}
