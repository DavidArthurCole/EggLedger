package eiafx

import (
	_ "embed"
	"fmt"

	"github.com/DavidArthurCole/EggLedger/ei"
	"google.golang.org/protobuf/encoding/protojson"
)

//go:embed eiafx-config-min.json
var _eiafxConfigJSON []byte

var Config *ei.ArtifactsConfigurationResponse

func LoadConfig() error {
	Config = &ei.ArtifactsConfigurationResponse{}
	err := protojson.Unmarshal(_eiafxConfigJSON, Config)
	if err != nil {
		return fmt.Errorf("error unmarshalling eiafx-config-min.json: %w", err)
	}
	return nil
}
