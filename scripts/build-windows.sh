#!/bin/bash -eu
exe=dist/EggLedger.exe
GOOS=windows GOARCH=amd64 CGO_ENABLED=1 CC=x86_64-w64-mingw32-gcc go build -trimpath -buildvcs=false -ldflags '-H windowsgui -s -w' -o $exe
echo "generated $exe"

cd dist
rm -f EggLedger-windows.zip
zip -1 -j EggLedger-windows.zip EggLedger.exe
cd ..
