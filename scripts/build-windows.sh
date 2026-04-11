#!/bin/bash -eu
exe=dist/EggLedger.exe
GOOS=windows GOARCH=amd64 go build -trimpath -buildvcs=false -ldflags '-H windowsgui -s -w' -o $exe
echo "generated $exe"
