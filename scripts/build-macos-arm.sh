#!/bin/bash -eu
app=dist/EggLedger.app
version=$(<VERSION)
echo "generating $app v${version}..."
rm -rf $app
mkdir -p $app/Contents/{MacOS,Resources}
export MACOSX_DEPLOYMENT_TARGET=13.0
GOOS=darwin GOARCH=arm64 \
  CGO_ENABLED=1 \
  go build -o $app/Contents/MacOS/EggLedger
cat > $app/Contents/Info.plist <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>EggLedger</string>
  <key>CFBundleIconFile</key>
  <string>icon.icns</string>
  <key>CFBundleIdentifier</key>
  <string>sh.tcl.EggLedger</string>
  <key>CFBundleVersion</key>
  <string>$version</string>
  <key>CFBundleShortVersionString</key>
  <string>$version</string>
</dict>
</plist>
EOF
cp assets/icons/icon.icns $app/Contents/Resources/icon.icns
echo "generated $app"

cd dist
rm -rf EggLedger-mac.zip EggLedger
mkdir EggLedger
cp -r EggLedger.app EggLedger/
cp ../README.macOS.html EggLedger/README.html
cat >>EggLedger/preflight <<'EOF'
#!/bin/zsh
setopt nounset errexit
promptexit () { read -sk1 '?[Press any key to exit]'; }
success () { echo $'\e[32m'$1$'\e[0m'; }
die () { echo $'\e[31m'"Error: $1"$'\e[0m' >&2; promptexit; exit 1; }
here=$0:A:h
app=$here/EggLedger.app
[[ -d $app ]] || die "EggLedger.app not found at $here"
echo "Removing com.apple.quarantine attribute from EggLedger.app..."
xattr -c $app || die "Failed to remove com.apple.quarantine from EggLedger.app"
success "Success! You can now launch EggLedger.app normally."
promptexit
EOF
chmod +x EggLedger/preflight
zip -r EggLedger-mac-arm64.zip EggLedger
rm -rf EggLedger
echo "generated dist/EggLedger-mac-arm64.zip"
cd ..
