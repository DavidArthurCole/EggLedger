#!/bin/bash -eu
bin=dist/eggledger-linux
GOOS=linux GOARCH=amd64 go build -buildvcs=false -ldflags '-s -w' -o $bin
echo "generated $bin"

cd dist
rm -f EggLedger-linux.tar.gz
tar -czf EggLedger-linux.tar.gz eggledger-linux
cd ..
