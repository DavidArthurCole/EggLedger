// Base on https://github.com/fanaticscripter/EggContractor/blob/ed17ac71316b34f77ec9b51a78e1ca3d9f11d35d/api/request.go

package api

import (
	"compress/gzip"
	"context"
	"encoding/base64"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"strings"
	"time"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
	"google.golang.org/protobuf/proto"

	"github.com/DavidArthurCole/EggLedger/ei"
)

// DebugCompression, when true, disables the transport's automatic gzip
// decompression so the raw Content-Encoding header can be logged. Set via
// the -debug-compression CLI flag.
var DebugCompression bool

const (
	AppVersion    = "1.35.5"
	AppBuild      = "111334"
	ClientVersion = 71
	Platform      = ei.Platform_IOS
)

const _apiPrefix = "https://ctx-dot-auxbrainhome.appspot.com"

var _client *http.Client
var _debugClient *http.Client

func init() {
	_client = &http.Client{
		Timeout: 5 * time.Second,
	}
	_debugClient = &http.Client{
		Timeout: 5 * time.Second,
		Transport: &http.Transport{
			// Disable automatic gzip decompression so the raw
			// Content-Encoding header is preserved for inspection.
			DisableCompression: true,
		},
	}
}

func Request(endpoint string, reqMsg proto.Message, respMsg proto.Message) error {
	return RequestWithContext(context.Background(), endpoint, reqMsg, respMsg)
}

func RequestWithContext(ctx context.Context, endpoint string, reqMsg proto.Message, respMsg proto.Message) error {
	return doRequestWithContext(ctx, endpoint, reqMsg, respMsg, false)
}

func RequestAuthenticated(endpoint string, reqMsg proto.Message, respMsg proto.Message) error {
	return RequestAuthenticatedWithContext(context.Background(), endpoint, reqMsg, respMsg)
}

func RequestAuthenticatedWithContext(ctx context.Context, endpoint string, reqMsg proto.Message, respMsg proto.Message) error {
	return doRequestWithContext(ctx, endpoint, reqMsg, respMsg, true)
}

func RequestRawPayload(endpoint string, reqMsg proto.Message) ([]byte, error) {
	return RequestRawPayloadWithContext(context.Background(), endpoint, reqMsg)
}

// Raw payload is the base64-decoded API response.
func RequestRawPayloadWithContext(ctx context.Context, endpoint string, reqMsg proto.Message) ([]byte, error) {
	return doRequestRawPayloadWithContext(ctx, endpoint, reqMsg)
}

func doRequestRawPayloadWithContext(ctx context.Context, endpoint string, reqMsg proto.Message) ([]byte, error) {
	apiUrl := _apiPrefix + endpoint
	reqBin, err := proto.Marshal(reqMsg)
	if err != nil {
		return nil, fmt.Errorf("marshaling payload %#v for %s: %w", reqMsg, apiUrl, err)
	}
	enc := base64.StdEncoding
	reqDataEncoded := enc.EncodeToString(reqBin)
	log.Infof("POST %s: %+v", apiUrl, reqMsg)
	log.Debugf("POST %s data=%s", apiUrl, reqDataEncoded)

	form := url.Values{"data": {reqDataEncoded}}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, apiUrl, strings.NewReader(form.Encode()))
	if err != nil {
		return nil, fmt.Errorf("creating POST request for %s: %w", apiUrl, err)
	}
	req.Header.Set("Content-Type", "application/x-www-form-urlencoded")

	client := _client
	if DebugCompression {
		client = _debugClient
	}
	resp, err := client.Do(req)
	if err != nil {
		if e, ok := err.(net.Error); ok && e.Timeout() {
			err = fmt.Errorf("timeout after %s", _client.Timeout.String())
		} else if errors.Is(err, context.Canceled) {
			err = fmt.Errorf("interrupted")
		}
		return nil, fmt.Errorf("POST %s: %w", apiUrl, err)
	}
	defer resp.Body.Close()

	var bodyReader io.Reader = resp.Body
	if DebugCompression {
		contentEncoding := resp.Header.Get("Content-Encoding")
		if contentEncoding == "" {
			log.Infof("[debug-compression] POST %s: Content-Encoding: (none) — server is not compressing responses", apiUrl)
		} else {
			log.Infof("[debug-compression] POST %s: Content-Encoding: %s", apiUrl, contentEncoding)
		}
		if strings.EqualFold(contentEncoding, "gzip") {
			gr, gzErr := gzip.NewReader(resp.Body)
			if gzErr != nil {
				return nil, fmt.Errorf("POST %s: opening gzip reader: %w", apiUrl, gzErr)
			}
			defer gr.Close()
			bodyReader = gr
		}
	}

	body, err := io.ReadAll(bodyReader)
	if err != nil {
		return nil, fmt.Errorf("POST %s: %w", apiUrl, err)
	}
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return nil, fmt.Errorf("POST %s: HTTP %d: %#v", apiUrl, resp.StatusCode, string(body))
	}

	decoded := make([]byte, base64.StdEncoding.DecodedLen(len(body)))
	n, err := base64.StdEncoding.Decode(decoded, body)
	if err != nil {
		return nil, fmt.Errorf("POST %s: %#v: base64 decode error: %w", apiUrl, string(body), err)
	}
	return decoded[:n], nil
}

func doRequestWithContext(ctx context.Context, endpoint string, reqMsg proto.Message, respMsg proto.Message, authenticated bool) error {
	apiUrl := _apiPrefix + endpoint
	payload, err := doRequestRawPayloadWithContext(ctx, endpoint, reqMsg)
	if err != nil {
		return err
	}
	return DecodeAPIResponse(apiUrl, payload, respMsg, authenticated)
}

func DecodeAPIResponse(apiUrl string, payload []byte, msg proto.Message, authenticated bool) error {
	var err error
	if authenticated {
		authMsg := &ei.AuthenticatedMessage{}
		err = proto.Unmarshal(payload, authMsg)
		if err != nil {
			err = fmt.Errorf("unmarshaling %s response as AuthenticatedMessage (%#v): %w", apiUrl, string(payload), err)
			return interpretUnmarshalError(err)
		}
		err = proto.Unmarshal(authMsg.Message, msg)
		if err != nil {
			err = fmt.Errorf("unmarshaling AuthenticatedMessage payload in %s response (%#v): %w", apiUrl, string(payload), err)
			return interpretUnmarshalError(err)
		}
	} else {
		err = proto.Unmarshal(payload, msg)
		if err != nil {
			err = fmt.Errorf("unmarshaling %s response (%#v): %w", apiUrl, string(payload), err)
			return interpretUnmarshalError(err)
		}
	}
	return nil
}

func interpretUnmarshalError(err error) error {
	if strings.Contains(err.Error(), "contains invalid UTF-8") {
		return fmt.Errorf("API returned corrupted data (invalid UTF-8 in one or more string fields); " +
			"this is a known issue affecting some players, and it can only be resolved when Auxbrain fixes their server bug")
	}
	return err
}

func NewBasicRequestInfo(userId string) *ei.BasicRequestInfo {
	return &ei.BasicRequestInfo{
		EiUserId:      &userId,
		ClientVersion: u32ptr(ClientVersion),
		Version:       sptr(AppVersion),
		Build:         sptr(AppBuild),
		Platform:      sptr(Platform.String()),
	}
}

func u32ptr(x uint32) *uint32 {
	return &x
}

func sptr(s string) *string {
	return &s
}
