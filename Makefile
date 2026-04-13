.PHONY: check build protobuf dev-app dist frontend typecheck lint sync-proto sync-eiafx

EIINC_PROTOS_DIR ?= ../EggIncProtos
EIINC_TOOLS_DIR  ?= ../EggIncAPITools

check:
	bash scripts/check-deps.sh

build:
	go build -o dist/EggLedger .

init:
	npm ci

frontend:
	npm run build

typecheck:
	npm run typecheck

lint:
	npm run lint

protobuf:
	protoc --proto_path=. --go_out=paths=source_relative:. ei/ei.proto

sync-proto:
	bash scripts/sync-proto.sh "$(EIINC_PROTOS_DIR)"

sync-eiafx:
	@if [ -z "$(EID)" ]; then echo "Usage: make sync-eiafx EID=EI0000000000000000"; exit 1; fi
	bash scripts/sync-eiafx.sh "$(EIINC_TOOLS_DIR)" "$(EID)"

dev-app: build
	echo EggLedger | DEV_MODE=1 entr -r ./dist/EggLedger

dist: frontend protobuf dist-windows dist-mac dist-linux dist-mac-arm

dist-windows: init frontend protobuf
	dos2unix ./scripts/build-windows.sh
	bash ./scripts/build-windows.sh

dist-linux: init frontend protobuf
	dos2unix ./scripts/build-linux.sh
	chmod +x ./scripts/build-linux.sh
	./scripts/build-linux.sh

dist-mac: init frontend protobuf
	dos2unix ./scripts/build-macos.sh
	chmod +x ./scripts/build-macos.sh
	./scripts/build-macos.sh

dist-mac-arm: init frontend protobuf
	dos2unix ./scripts/build-macos-arm.sh
	chmod +x ./scripts/build-macos-arm.sh
	./scripts/build-macos-arm.sh
