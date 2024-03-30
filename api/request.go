// Base on https://github.com/fanaticscripter/EggContractor/blob/ed17ac71316b34f77ec9b51a78e1ca3d9f11d35d/api/request.go

package api

import (
	"context"
	"encoding/base64"
	"io"
	"net"
	"net/http"
	"net/url"
	"strings"
	"time"

	"github.com/pkg/errors"
	log "github.com/sirupsen/logrus"
	"golang.org/x/net/context/ctxhttp"
	"google.golang.org/protobuf/proto"

	"github.com/DavidArthurCole/EggLedger/ei"
)

const (
	AppVersion     = "1.30"
	AppBuild       = "1.30.0.0"
	ClientVersion  = 64
	PlatformString = "IOS"
	Platform       = ei.Platform_IOS
)

const _apiPrefix = "https://ctx-dot-auxbrainhome.appspot.com"

var _client *http.Client

func init() {
	_client = &http.Client{
		Timeout: 5 * time.Second,
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
		return nil, errors.Wrapf(err, "marshaling payload %+v for %s", reqMsg, apiUrl)
	}
	enc := base64.StdEncoding
	reqDataEncoded := enc.EncodeToString(reqBin)
	log.Infof("POST %s: %+v", apiUrl, reqMsg)
	log.Debugf("POST %s data=%s", apiUrl, reqDataEncoded)
	resp, err := ctxhttp.PostForm(ctx, _client, apiUrl, url.Values{"data": {reqDataEncoded}})
	if err != nil {
		if e, ok := err.(net.Error); ok && e.Timeout() {
			err = errors.Errorf("timeout after %s", _client.Timeout.String())
		} else if errors.Is(err, context.Canceled) {
			err = errors.New("interrupted")
		}
		return nil, errors.Wrapf(err, "POST %s", apiUrl)
	}
	defer resp.Body.Close()
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, errors.Wrapf(err, "POST %s", apiUrl)
	}
	if !(resp.StatusCode >= 200 && resp.StatusCode < 300) {
		return nil, errors.Errorf("POST %s: HTTP %d: %#v", apiUrl, resp.StatusCode, string(body))
	}
	buf := make([]byte, enc.DecodedLen(len(body)))
	n, err := enc.Decode(buf, body)
	if err != nil {
		return nil, errors.Wrapf(err, "POST %s: %#v: base64 decode error", apiUrl, string(body))
	}
	return buf[:n], nil
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
			err = errors.Wrapf(err, "unmarshaling %s response as AuthenticatedMessage (%#v)", apiUrl, string(payload))
			return interpretUnmarshalError(err)
		}
		err = proto.Unmarshal(authMsg.Message, msg)
		if err != nil {
			err = errors.Wrapf(err, "unmarshaling AuthenticatedMessage payload in %s response (%#v)", apiUrl, string(payload))
			return interpretUnmarshalError(err)
		}
	} else {
		err = proto.Unmarshal(payload, msg)
		if err != nil {
			err = errors.Wrapf(err, "unmarshaling %s response (%#v)", apiUrl, string(payload))
			return interpretUnmarshalError(err)
		}
	}
	return nil
}

func interpretUnmarshalError(err error) error {
	if strings.Contains(err.Error(), "contains invalid UTF-8") {
		return errors.Wrap(err, "API returned corrupted data (invalid UTF-8 in one or more string fields); "+
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
		Platform:      sptr(PlatformString),
	}
}

func u32ptr(x uint32) *uint32 {
	return &x
}

func sptr(s string) *string {
	return &s
}
