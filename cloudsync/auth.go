package cloudsync

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"time"

	log "github.com/sirupsen/logrus"
	"github.com/skratchdot/open-golang/open"
)

// authInitResponse is returned by GET /auth/discord.
type authInitResponse struct {
	State string `json:"state"`
	URL   string `json:"url"`
}

// pollResponse is the shape of a successful GET /auth/poll response.
type pollResponse struct {
	Token         string `json:"token"`
	Username      string `json:"username"`
	AvatarURL     string `json:"avatarUrl"`
	EncryptionKey string `json:"encryptionKey"`
}

// ConnectDiscord starts the Discord OAuth2 polling flow. It asks the
// server for the auth URL and state, opens the URL in the system browser, then
// polls /auth/poll?state=<state> until the user authenticates or the 5-minute
// window expires. Fires onDiscordAuthComplete when done. Returns the Discord
// authorization URL for the UI fallback link.
func ConnectDiscord() (string, error) {
	client := _cloudClient
	resp, err := client.Get(_cloudSyncBaseURL + "/auth/discord")
	if err != nil {
		return "", fmt.Errorf("connectDiscord: init: %w", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("connectDiscord: server returned %d", resp.StatusCode)
	}
	var init authInitResponse
	if err := json.NewDecoder(resp.Body).Decode(&init); err != nil {
		return "", fmt.Errorf("connectDiscord: decode: %w", err)
	}

	if err := open.Start(init.URL); err != nil {
		log.Warnf("cloud sync: could not open browser: %v", err)
	}

	go func() {
		ctx, cancel := context.WithTimeout(context.Background(), _cloudAuthPollTimeout)
		defer cancel()

		ticker := time.NewTicker(_cloudAuthPollInterval)
		defer ticker.Stop()

		for {
			select {
			case <-ctx.Done():
				log.Warn("cloud sync: auth polling timed out")
				if onDiscordAuthComplete != nil {
					onDiscordAuthComplete(false, "")
				}
				return
			case <-ticker.C:
				token, username, avatarURL, encryptionKey, pollErr := pollAuthToken(ctx, init.State)
				if pollErr != nil {
					log.Debugf("cloud sync: poll error: %v", pollErr)
					continue
				}
				if token == "" {
					continue
				}
				_storage.SetCloudSessionToken(token)
				_storage.SetCloudEncryptionKey(encryptionKey)
				_storage.SetCloudDiscordUsername(username)
				_storage.SetCloudDiscordAvatarURL(avatarURL)
				log.Infof("cloud sync: authenticated as %s", username)
				if onDiscordAuthComplete != nil {
					onDiscordAuthComplete(true, username)
				}
				return
			}
		}
	}()

	return init.URL, nil
}

// pollAuthToken calls GET /auth/poll?state=<state> once.
// Returns ("", "", "", "", nil) while still pending, (token, username, avatarURL, encryptionKey, nil) on success,
// ("", "", "", "", err) on a definitive server error.
func pollAuthToken(ctx context.Context, state string) (string, string, string, string, error) {
	req, err := http.NewRequestWithContext(ctx, http.MethodGet,
		_cloudSyncBaseURL+"/auth/poll?state="+state, nil)
	if err != nil {
		return "", "", "", "", err
	}
	client := _cloudClient
	resp, err := client.Do(req)
	if err != nil {
		return "", "", "", "", err
	}
	defer resp.Body.Close()

	switch resp.StatusCode {
	case http.StatusAccepted, http.StatusNotFound:
		return "", "", "", "", nil
	case http.StatusOK:
	default:
		return "", "", "", "", fmt.Errorf("poll: unexpected status %d", resp.StatusCode)
	}

	var pr pollResponse
	if err := json.NewDecoder(resp.Body).Decode(&pr); err != nil {
		return "", "", "", "", fmt.Errorf("poll: decode: %w", err)
	}
	return pr.Token, pr.Username, pr.AvatarURL, pr.EncryptionKey, nil
}

// DisconnectCloud invalidates the server-side session and clears local creds.
func DisconnectCloud() {
	token := _storage.GetCloudSessionToken()
	if token != "" {
		go func() {
			req, err := http.NewRequest(http.MethodDelete, _cloudSyncBaseURL+"/auth/session", nil)
			if err != nil {
				log.Warnf("cloud sync: disconnect: %v", err)
				return
			}
			req.Header.Set("Authorization", "Bearer "+token)
			client := _cloudClient
			resp, err := client.Do(req)
			if err != nil {
				log.Warnf("cloud sync: disconnect: %v", err)
				return
			}
			resp.Body.Close()
		}()
	}
	_storage.SetCloudSessionToken("")
	_storage.SetCloudDiscordUsername("")
}
