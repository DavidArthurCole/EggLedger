.PHONY: check build css protobuf dev-app dev-css dist

check:
	bash scripts/check-deps.sh

build:
	go build -o dist/EggLedger .

init:
	npm ci

css:
	npm run build:css

protobuf:
	protoc --proto_path=. --go_out=paths=source_relative:. ei/ei.proto

dev-app: build
	echo EggLedger | DEV_MODE=1 entr -r ./dist/EggLedger

dev-css:
	npm run dev:css

dist: css protobuf dist-windows dist-mac dist-linux dist-mac-arm

dist-windows: init css protobuf
	dos2unix ./scripts/build-windows.sh
	chmod +x ./scripts/build-windows.sh
	./scripts/build-windows.sh

dist-linux: init css protobuf
	dos2unix ./scripts/build-linux.sh
	chmod +x ./scripts/build-linux.sh
	./scripts/build-linux.sh

dist-mac: init css protobuf
	dos2unix ./scripts/build-macos.sh
	chmod +x ./scripts/build-macos.sh
	./scripts/build-macos.sh

dist-mac-arm: init css protobuf
	dos2unix ./scripts/build-macos-arm.sh
	chmod +x ./scripts/build-macos-arm.sh
	./scripts/build-macos-arm.sh
