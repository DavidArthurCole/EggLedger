package utils

import (
	"math"
	"time"
)

func TimeToUnix(t time.Time) float64 {
	return float64(t.UnixNano()) / 1e9
}

func UnixToTime(t float64) time.Time {
	sec, dec := math.Modf(t)
	return time.Unix(int64(sec), int64(dec*1e9))
}

func Sum[T any](slice []T, toFloat func(T) float64) float64 {
	var total float64
	for _, v := range slice {
		total += toFloat(v)
	}
	return total
}
