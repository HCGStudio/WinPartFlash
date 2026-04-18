#!/bin/sh
# Sign + notarize + staple a WinPartFlash.app and produce a signed .dmg.
#
# Driven by environment variables so it can run identically in CI and locally:
#   APPLE_DEVELOPER_ID_NAME      Full identity, e.g.
#                                "Developer ID Application: Jane Doe (ABCDE12345)"
#   APPLE_TEAM_ID                10-char Team ID (e.g. ABCDE12345)
#   APPLE_API_KEY_ID             App Store Connect API key ID (10 chars).
#   APPLE_API_KEY_ISSUER_ID      Issuer ID (UUID) from App Store Connect.
#   APPLE_API_KEY_P8_BASE64      Base64-encoded contents of the .p8 private key
#                                downloaded from App Store Connect.
#
# Optional:
#   APPLE_DEVELOPER_ID_P12_BASE64    Base64-encoded .p12; if set, the script
#                                    imports it into a temporary keychain.
#                                    Required in CI; skip locally if the
#                                    identity is already in your login keychain.
#   APPLE_DEVELOPER_ID_P12_PASSWORD  Password for that .p12.
#
# Usage:
#   ./scripts/macos_sign_and_register.sh <publish-dir> [version]
#
# The publish dir must contain WinPartFlash.app (produced by macos_package.sh).
# A signed, notarized, stapled WinPartFlash-<version>.dmg is left next to it.

set -e

PUBLISH_DIR="$1"
VERSION="${2:-0.0.0}"

if [ -z "$PUBLISH_DIR" ] || [ ! -d "$PUBLISH_DIR/WinPartFlash.app" ]; then
    echo "Usage: $0 <publish-dir> [version]"
    echo "Expected $PUBLISH_DIR/WinPartFlash.app to exist."
    exit 1
fi

for var in APPLE_DEVELOPER_ID_NAME APPLE_TEAM_ID APPLE_API_KEY_ID APPLE_API_KEY_ISSUER_ID APPLE_API_KEY_P8_BASE64; do
    eval "value=\$$var"
    if [ -z "$value" ]; then
        echo "Missing required env var: $var"
        exit 1
    fi
done

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
APP="$PUBLISH_DIR/WinPartFlash.app"
DMG="$PUBLISH_DIR/WinPartFlash-$VERSION.dmg"
GUI_ENTS="$SCRIPT_DIR/../MacOS/entitlements.gui.plist"
HELPER_ENTS="$SCRIPT_DIR/../MacOS/entitlements.helper.plist"

KEYCHAIN_PATH=""
API_KEY_PATH=""
cleanup() {
    if [ -n "$KEYCHAIN_PATH" ] && [ -f "$KEYCHAIN_PATH" ]; then
        security delete-keychain "$KEYCHAIN_PATH" 2>/dev/null || true
    fi
    if [ -n "$API_KEY_PATH" ] && [ -f "$API_KEY_PATH" ]; then
        rm -f "$API_KEY_PATH"
    fi
}
trap cleanup EXIT

# Materialise the App Store Connect API key so notarytool can read it.
API_KEY_PATH=$(mktemp -t winpartflash-api-key).p8
echo "$APPLE_API_KEY_P8_BASE64" | base64 --decode > "$API_KEY_PATH"

if [ -n "$APPLE_DEVELOPER_ID_P12_BASE64" ]; then
    # CI path: materialise a throwaway keychain so we don't pollute login.keychain.
    if [ -z "$APPLE_DEVELOPER_ID_P12_PASSWORD" ]; then
        echo "APPLE_DEVELOPER_ID_P12_BASE64 set but APPLE_DEVELOPER_ID_P12_PASSWORD missing"
        exit 1
    fi
    KEYCHAIN_PATH="$RUNNER_TEMP/winpartflash-signing.keychain-db"
    [ -z "$RUNNER_TEMP" ] && KEYCHAIN_PATH="/tmp/winpartflash-signing.keychain-db"
    KEYCHAIN_PASS=$(uuidgen)
    P12_PATH=$(mktemp -t winpartflash-cert).p12
    echo "$APPLE_DEVELOPER_ID_P12_BASE64" | base64 --decode > "$P12_PATH"

    security create-keychain -p "$KEYCHAIN_PASS" "$KEYCHAIN_PATH"
    security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
    security unlock-keychain -p "$KEYCHAIN_PASS" "$KEYCHAIN_PATH"
    security import "$P12_PATH" -k "$KEYCHAIN_PATH" \
        -P "$APPLE_DEVELOPER_ID_P12_PASSWORD" \
        -T /usr/bin/codesign -T /usr/bin/security
    # Allow codesign to use the key without an interactive prompt.
    security set-key-partition-list -S apple-tool:,apple: -s -k "$KEYCHAIN_PASS" "$KEYCHAIN_PATH"
    # Put our temp keychain ahead of login.keychain in the search list.
    security list-keychains -d user -s "$KEYCHAIN_PATH" $(security list-keychains -d user | sed -e 's/"//g')
    rm -f "$P12_PATH"
fi

# Substitute the real Team ID into the LaunchDaemon plist's SMAuthorizedClients
# requirement (it ships with a TEAMID placeholder).
LD_PLIST="$APP/Contents/Library/LaunchDaemons/com.hcgstudio.winpartflash.helper.plist"
if [ -f "$LD_PLIST" ]; then
    sed -i.bak -e "s/TEAMID/$APPLE_TEAM_ID/g" "$LD_PLIST"
    rm -f "$LD_PLIST.bak"
fi

# Sign inside-out: helper binary, native dylib, main executable, then the
# bundle.  --options runtime is mandatory for notarization.
codesign --force --options runtime --timestamp \
    --sign "$APPLE_DEVELOPER_ID_NAME" \
    --entitlements "$HELPER_ENTS" \
    "$APP/Contents/MacOS/com.hcgstudio.winpartflash.helper"

codesign --force --options runtime --timestamp \
    --sign "$APPLE_DEVELOPER_ID_NAME" \
    "$APP/Contents/MacOS/libWinPartFlashLib.dylib"

# Sign every other Mach-O that publish dropped (Avalonia natives, etc.) so the
# outer --deep pass doesn't reject anything.
find "$APP/Contents/MacOS" -type f \( -name '*.dylib' -o -name '*.so' \) \
    ! -name 'libWinPartFlashLib.dylib' -print0 | \
    xargs -0 -I{} codesign --force --options runtime --timestamp \
        --sign "$APPLE_DEVELOPER_ID_NAME" "{}"

codesign --force --options runtime --timestamp \
    --sign "$APPLE_DEVELOPER_ID_NAME" \
    --entitlements "$GUI_ENTS" \
    "$APP/Contents/MacOS/WinPartFlash.Gui"

codesign --force --options runtime --timestamp \
    --sign "$APPLE_DEVELOPER_ID_NAME" \
    --entitlements "$GUI_ENTS" \
    "$APP"

codesign --verify --strict --verbose=2 "$APP"

# Rebuild the dmg from the now-signed bundle so the image contains signed bits.
rm -f "$DMG"
hdiutil create -volname "WinPartFlash" -srcfolder "$APP" -ov -format UDZO "$DMG"
codesign --force --timestamp --sign "$APPLE_DEVELOPER_ID_NAME" "$DMG"

# Submit to Apple notary service and wait for the verdict.
xcrun notarytool submit "$DMG" \
    --key "$API_KEY_PATH" \
    --key-id "$APPLE_API_KEY_ID" \
    --issuer "$APPLE_API_KEY_ISSUER_ID" \
    --wait

# Staple the notarization ticket so Gatekeeper accepts the dmg offline.
xcrun stapler staple "$DMG"
xcrun stapler validate "$DMG"

echo "Signed + notarized: $DMG"
