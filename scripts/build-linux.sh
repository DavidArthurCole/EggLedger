#!/bin/bash -eu
bin=dist/eggledger-linux
GOOS=linux GOARCH=amd64 CGO_ENABLED=1 go build -o $bin
echo "generated $bin"

cd dist
rm -rf EggLedger-linux.tar.gz EggLedger
mkdir EggLedger
cp eggledger-linux EggLedger/
tar -czf EggLedger-linux.tar.gz EggLedger/
rm -rf EggLedger
echo "generated dist/EggLedger-linux.tar.gz"
cd ..
