package main

import (
	"fmt"
	"math"
	"time"
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

type FarmerRole struct {
	oom   int
	name  string
	color string
}

var FarmerRoles = []FarmerRole{
	{oom: 0, name: "Farmer", color: "d43500"},
	{oom: 1, name: "Farmer II", color: "d14400"},
	{oom: 2, name: "Farmer III", color: "cd5500"},
	{oom: 3, name: "Kilofarmer", color: "ca6800"},
	{oom: 4, name: "Kilofarmer II", color: "c77a00"},
	{oom: 5, name: "Kilofarmer III", color: "c58a00"},
	{oom: 6, name: "Megafarmer", color: "c49400"},
	{oom: 7, name: "Megafarmer II", color: "c39f00"},
	{oom: 8, name: "Megafarmer III", color: "c3a900"},
	{oom: 9, name: "Gigafarmer", color: "c2b100"},
	{oom: 10, name: "Gigafarmer II", color: "c2ba00"},
	{oom: 11, name: "Gigafarmer III", color: "c2c200"},
	{oom: 12, name: "Terafarmer", color: "aec300"},
	{oom: 13, name: "Terafarmer II", color: "99c400"},
	{oom: 14, name: "Terafarmer III", color: "85c600"},
	{oom: 15, name: "Petafarmer", color: "51ce00"},
	{oom: 16, name: "Petafarmer II", color: "16dc00"},
	{oom: 17, name: "Petafarmer III", color: "00ec2e"},
	{oom: 18, name: "Exafarmer", color: "00fa68"},
	{oom: 19, name: "Exafarmer II", color: "0afc9c"},
	{oom: 20, name: "Exafarmer III", color: "1cf7ca"},
	{oom: 21, name: "Zettafarmer", color: "2af3eb"},
	{oom: 22, name: "Zettafarmer II", color: "35d9f0"},
	{oom: 23, name: "Zettafarmer III", color: "40bced"},
	{oom: 24, name: "Yottafarmer", color: "46a8eb"},
	{oom: 25, name: "Yottafarmer II", color: "4a9aea"},
	{oom: 26, name: "Yottafarmer III", color: "4e8dea"},
	{oom: 27, name: "Xennafarmer", color: "527ce9"},
	{oom: 28, name: "Xennafarmer II", color: "5463e8"},
	{oom: 29, name: "Xennafarmer III", color: "6155e8"},
	{oom: 30, name: "Weccafarmer", color: "7952e9"},
	{oom: 31, name: "Weccafarmer II", color: "8b4fe9"},
	{oom: 32, name: "Weccafarmer III", color: "9d4aeb"},
	{oom: 33, name: "Vendafarmer", color: "b343ec"},
	{oom: 34, name: "Vendafarmer II", color: "d636ef"},
	{oom: 35, name: "Vendafarmer III", color: "f327e5"},
	{oom: 36, name: "Uadafarmer", color: "f915ba"},
	{oom: 37, name: "Uadafarmer II", color: "fc0a9c"},
	{oom: 38, name: "Uadafarmer III", color: "ff007d"},
	{oom: 39, name: "Treidafarmer", color: "f7005d"},
	{oom: 40, name: "Treidafarmer II", color: "f61fd2"},
	{oom: 41, name: "Treidafarmer III", color: "9c4aea"},
	{oom: 42, name: "Quadafarmer", color: "5559e8"},
	{oom: 43, name: "Quadafarmer II", color: "4a9deb"},
	{oom: 44, name: "Quadafarmer III", color: "2df0f2"},
	{oom: 45, name: "Pendafarmer", color: "00f759"},
	{oom: 46, name: "Pendafarmer II", color: "7ec700"},
	{oom: 47, name: "Pendafarmer III", color: "c2bf00"},
	{oom: 48, name: "Exedafarmer", color: "c3a000"},
	{oom: 49, name: "Exedafarmer II", color: "c87200"},
	{oom: 50, name: "Exedafarmer III", color: "d43500"},
	{oom: 51, name: "Infinifarmer", color: "546e7a"},
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

	for _, role := range FarmerRoles {
		if ((ooms * 3) - precision) == role.oom {
			//Return role and addendum
			return role.color, role.name, Addendum(ooms), earningsBonusCopy, precision
		}
	}
	role := FarmerRoles[len(FarmerRoles)-1]
	return role.color, role.name, Addendum(FarmerRoles[len(FarmerRoles)-1].oom), earningsBonusCopy, precision
}
