#!/bin/bash -eu
exe=dist/EggLedger.exe
GOOS=windows GOARCH=amd64 CGO_ENABLED=1 CC=x86_64-w64-mingw32-gcc go build -ldflags '-H windowsgui' -o $exe
echo "generated $exe"

cd dist
rm -rf EggLedger-windows.zip EggLedger
mkdir EggLedger
cp EggLedger.exe EggLedger/
zip -r EggLedger-windows.zip EggLedger
rm -rf EggLedger
echo "generated dist/EggLedger-windows.zip"
cd ..
