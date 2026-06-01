package update

import "flag"

var (
	_replacePID       int
	_replacePath      string
	_handshakePort    string
	_handshakeToken   string
	_forceUpdateCheck bool
)

func init() {
	flag.IntVar(&_replacePID, "replace-pid", 0, "")
	flag.StringVar(&_replacePath, "replace-path", "", "")
	flag.StringVar(&_handshakePort, "handshake-port", "", "")
	flag.StringVar(&_handshakeToken, "handshake-token", "", "")
	flag.BoolVar(&_forceUpdateCheck, "force-update-check", false, "bypass the 12-hour update-check cooldown")
}
