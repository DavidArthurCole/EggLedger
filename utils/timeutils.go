package utils

import (
	"fmt"
	"time"
)

// HumanizeTime returns a human-readable relative time string (e.g., "3 hours ago").
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
