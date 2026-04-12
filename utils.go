package main

import (
	"fmt"
	"math"
	"time"

	"github.com/DavidArthurCole/EggLedger/ledgerdata"
)

func timeToUnix(t time.Time) float64 {
	return float64(t.UnixNano()) / 1e9
}

func unixToTime(t float64) time.Time {
	sec, dec := math.Modf(t)
	return time.Unix(int64(sec), int64(dec*1e9))
}

func HumanizeTime(t time.Time) string {
	delta := time.Since(t)
	if delta < time.Minute {
		return "just now"
	} else if delta < time.Hour {
		return fmt.Sprintf("%d minutes ago", int(delta.Minutes()))
	} else if delta < 24*time.Hour {
		return fmt.Sprintf("%d hours ago", int(delta.Hours()))
	} else if delta < 30*24*time.Hour {
		return fmt.Sprintf("%d days ago", int(delta.Hours()/24))
	} else if delta < 365*24*time.Hour {
		return fmt.Sprintf("%d months ago", int(delta.Hours()/(24*30)))
	}
	return fmt.Sprintf("%d years ago", int(delta.Hours()/(24*365)))
}


/*
1000 -> K
1000000 -> M
1000000000 -> B
1000000000000 -> T
1000000000000000 -> q
.. -> Q
.. -> s
.. -> S
.. -> o
.. -> N
.. -> d
.. -> U
.. -> D
.. -> Td
.. -> qd
.. -> Qd
.. -> sd
.. -> Sd
.. -> od
__ -> !
*/
func Addendum(oom int) string {
	if oom > 20 {
		return "!"
	}
	return []string{
		"", "K", "M", "B", "T", "q", "Q", "s", "S", "o", "N", "d", "U", "D", "Td", "qd", "Qd", "sd", "Sd", "od", "Nd",
	}[oom]
}

// AbbreviateFloat formats a large float64 as a short human-readable string,
// e.g. 1234567890 -> "1.23B". Uses the same suffix table as Addendum.
func AbbreviateFloat(v float64) string {
	vCopy := v
	ooms := 0
	for vCopy >= 1e3 && ooms < 20 {
		vCopy /= 1e3
		ooms++
	}
	var precision int
	switch {
	case vCopy < 10.0:
		precision = 2
	case vCopy < 100.0:
		precision = 1
	default:
		precision = 0
	}
	format := fmt.Sprintf("%%.%df", precision)
	return fmt.Sprintf(format, vCopy) + Addendum(ooms)
}

func RoleFromEB(earningsBonus float64) (string, string, string, float64, int) {

	earningsBonusCopy := earningsBonus
	ooms := 0
	for earningsBonusCopy >= 1e3 && ooms < 17 {
		earningsBonusCopy /= 1e3
		ooms++
	}
	var precision int
	switch {
	case earningsBonusCopy < 10.0:
		precision = 2
	case earningsBonusCopy < 100.0:
		precision = 1
	default:
		precision = 0
	}

	roles := ledgerdata.Config.FarmerRoles
	for _, role := range roles {
		if ((ooms * 3) - precision) == role.OOM {
			//Return role and addendum
			return role.Color, role.Name, Addendum(ooms), earningsBonusCopy, precision
		}
	}
	role := roles[len(roles)-1]
	return role.Color, role.Name, Addendum(roles[len(roles)-1].OOM), earningsBonusCopy, precision
}
