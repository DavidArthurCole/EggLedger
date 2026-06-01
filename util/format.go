package util

import (
	"fmt"
)

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
