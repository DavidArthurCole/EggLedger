package util

import (
	"fmt"
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
