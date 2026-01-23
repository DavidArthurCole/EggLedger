package eiafx

import (
	_ "embed"

	"github.com/DavidArthurCole/EggLedger/ei"
	"github.com/pkg/errors"
	"google.golang.org/protobuf/encoding/protojson"
)

//go:embed eiafx-config-min.json
var _eiafxConfigJSON []byte

var Config *ei.ArtifactsConfigurationResponse

func LoadConfig() error {
	Config = &ei.ArtifactsConfigurationResponse{}
	err := protojson.Unmarshal(_eiafxConfigJSON, Config)
	if err != nil {
		return errors.Wrap(err, "error unmarshalling eiafx-config-min.json")
	}
	return nil
}
