package main

import (
	"testing"
	"time"

	"github.com/DavidArthurCole/EggLedger/ledgerdata"
)

func setupLedgerDataForUtils(t *testing.T) {
	t.Helper()
	if err := ledgerdata.LoadConfig(t.TempDir()); err != nil {
		t.Fatalf("ledgerdata.LoadConfig: %v", err)
	}
}

// Addendum

func TestAddendum_Zero(t *testing.T) {
	if got := Addendum(0); got != "" {
		t.Errorf("Addendum(0) = %q, want %q", got, "")
	}
}

func TestAddendum_K(t *testing.T) {
	if got := Addendum(1); got != "K" {
		t.Errorf("Addendum(1) = %q, want %q", got, "K")
	}
}

func TestAddendum_M(t *testing.T) {
	if got := Addendum(2); got != "M" {
		t.Errorf("Addendum(2) = %q, want %q", got, "M")
	}
}

func TestAddendum_T(t *testing.T) {
	if got := Addendum(4); got != "T" {
		t.Errorf("Addendum(4) = %q, want %q", got, "T")
	}
}

func TestAddendum_AboveCap(t *testing.T) {
	if got := Addendum(21); got != "!" {
		t.Errorf("Addendum(21) = %q, want %q", got, "!")
	}
}

// AbbreviateFloat

func TestAbbreviateFloat_BelowThousand(t *testing.T) {
	if got := AbbreviateFloat(999); got != "999" {
		t.Errorf("AbbreviateFloat(999) = %q, want %q", got, "999")
	}
}

func TestAbbreviateFloat_Kilo(t *testing.T) {
	if got := AbbreviateFloat(1234); got != "1.23K" {
		t.Errorf("AbbreviateFloat(1234) = %q, want %q", got, "1.23K")
	}
}

func TestAbbreviateFloat_Billion(t *testing.T) {
	if got := AbbreviateFloat(1_234_567_890); got != "1.23B" {
		t.Errorf("AbbreviateFloat(1234567890) = %q, want %q", got, "1.23B")
	}
}

func TestAbbreviateFloat_TenBillion(t *testing.T) {
	// 10B: vCopy=10.0, precision=1 -> "10.0B"
	if got := AbbreviateFloat(10_000_000_000); got != "10.0B" {
		t.Errorf("AbbreviateFloat(10B) = %q, want %q", got, "10.0B")
	}
}

func TestAbbreviateFloat_HundredBillion(t *testing.T) {
	// 100B: vCopy=100.0, precision=0 -> "100B"
	if got := AbbreviateFloat(100_000_000_000); got != "100B" {
		t.Errorf("AbbreviateFloat(100B) = %q, want %q", got, "100B")
	}
}

// HumanizeTime

func TestHumanizeTime_JustNow(t *testing.T) {
	if got := HumanizeTime(time.Now().Add(-10 * time.Second)); got != "just now" {
		t.Errorf("HumanizeTime(30s ago) = %q, want %q", got, "just now")
	}
}

func TestHumanizeTime_Minutes(t *testing.T) {
	if got := HumanizeTime(time.Now().Add(-5 * time.Minute)); got != "5 minutes ago" {
		t.Errorf("HumanizeTime(5m ago) = %q, want %q", got, "5 minutes ago")
	}
}

func TestHumanizeTime_Hours(t *testing.T) {
	if got := HumanizeTime(time.Now().Add(-3 * time.Hour)); got != "3 hours ago" {
		t.Errorf("HumanizeTime(3h ago) = %q, want %q", got, "3 hours ago")
	}
}

func TestHumanizeTime_Days(t *testing.T) {
	if got := HumanizeTime(time.Now().Add(-48 * time.Hour)); got != "2 days ago" {
		t.Errorf("HumanizeTime(48h ago) = %q, want %q", got, "2 days ago")
	}
}

func TestHumanizeTime_Months(t *testing.T) {
	// 45 days = 1 month by the formula (45*24 / (24*30) = 1)
	if got := HumanizeTime(time.Now().Add(-45 * 24 * time.Hour)); got != "1 months ago" {
		t.Errorf("HumanizeTime(45d ago) = %q, want %q", got, "1 months ago")
	}
}

func TestHumanizeTime_Years(t *testing.T) {
	if got := HumanizeTime(time.Now().Add(-400 * 24 * time.Hour)); got != "1 years ago" {
		t.Errorf("HumanizeTime(400d ago) = %q, want %q", got, "1 years ago")
	}
}

// RoleFromEB

func TestRoleFromEB_Farmer(t *testing.T) {
	setupLedgerDataForUtils(t)
	// EB=100: ooms=0, earningsBonusCopy=100.0, precision=0, OOM index=0 -> "Farmer"
	color, name, addendum, _, _ := RoleFromEB(100.0)
	if name != "Farmer" {
		t.Errorf("RoleFromEB(100) name = %q, want %q", name, "Farmer")
	}
	if color != "d43500" {
		t.Errorf("RoleFromEB(100) color = %q, want %q", color, "d43500")
	}
	if addendum != "" {
		t.Errorf("RoleFromEB(100) addendum = %q, want %q", addendum, "")
	}
}

func TestRoleFromEB_FarmerII(t *testing.T) {
	setupLedgerDataForUtils(t)
	// EB=1000: ooms=1, earningsBonusCopy=1.0, precision=2, OOM index=1 -> "Farmer II"
	_, name, addendum, val, _ := RoleFromEB(1000.0)
	if name != "Farmer II" {
		t.Errorf("RoleFromEB(1000) name = %q, want %q", name, "Farmer II")
	}
	if addendum != "K" {
		t.Errorf("RoleFromEB(1000) addendum = %q, want %q", addendum, "K")
	}
	if val != 1.0 {
		t.Errorf("RoleFromEB(1000) val = %f, want 1.0", val)
	}
}

func TestRoleFromEB_Kilofarmer(t *testing.T) {
	setupLedgerDataForUtils(t)
	// EB=100000: ooms=1, earningsBonusCopy=100.0, precision=0, OOM index=3 -> "Kilofarmer"
	_, name, _, _, _ := RoleFromEB(100000.0)
	if name != "Kilofarmer" {
		t.Errorf("RoleFromEB(100000) name = %q, want %q", name, "Kilofarmer")
	}
}
