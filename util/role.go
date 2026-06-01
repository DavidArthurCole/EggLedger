package util

import (
	"github.com/DavidArthurCole/EggLedger/ledgerdata"
)

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
