package main

import "testing"

func TestGetWorkerCount_Default(t *testing.T) {
	s := AppStorage{}
	if got := s.GetWorkerCount(); got != 1 {
		t.Errorf("expected default 1, got %d", got)
	}
}

func TestGetWorkerCount_Zero(t *testing.T) {
	s := AppStorage{WorkerCount: 0}
	if got := s.GetWorkerCount(); got != 1 {
		t.Errorf("expected 1 for zero value, got %d", got)
	}
}

func TestGetWorkerCount_Valid(t *testing.T) {
	s := AppStorage{WorkerCount: 5}
	if got := s.GetWorkerCount(); got != 5 {
		t.Errorf("expected 5, got %d", got)
	}
}

func TestGetWorkerCount_AboveMax(t *testing.T) {
	s := AppStorage{WorkerCount: 99}
	if got := s.GetWorkerCount(); got != 10 {
		t.Errorf("expected 10 for value 99, got %d", got)
	}
}
