#!/bin/sh
# Assemble WinPartFlash.app from a `dotnet publish` output directory and wrap
# it in a distributable .dmg.  Invoked automatically by the PackageMac MSBuild
# target after Publish on macOS hosts targeting osx-* runtimes.
#
# Usage:
#   ./scripts/macos_package.sh <publish-dir> <executable-name> [version]
#
# Layout produced:
#   <publish-dir>/WinPartFlash.app/
#       Contents/Info.plist
#       Contents/MacOS/<everything from publish-dir, minus the helper plist>
#       Contents/Library/LaunchDaemons/com.hcgstudio.winpartflash.helper.plist
#   <publish-dir>/WinPartFlash-<version>.dmg

set -e

PUBLISH_DIR="$1"
EXECUTABLE="$2"
VERSION="${3:-0.0.0}"

if [ -z "$PUBLISH_DIR" ] || [ -z "$EXECUTABLE" ]; then
    echo "Usage: $0 <publish-dir> <executable-name> [version]"
    exit 1
fi
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "Publish directory not found: $PUBLISH_DIR"
    exit 1
fi

SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)
TEMPLATE="$SCRIPT_DIR/../MacOS/Info.plist.template"
APP="$PUBLISH_DIR/WinPartFlash.app"
DMG="$PUBLISH_DIR/WinPartFlash-$VERSION.dmg"

# Reset any prior bundle so we don't end up with stale files inside Contents/MacOS.
rm -rf "$APP" "$DMG"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Library/LaunchDaemons" "$APP/Contents/Resources"

# Move publish payload into Contents/MacOS.  The helper plist is the only file
# that has to live elsewhere inside the bundle (LaunchDaemons), so peel it off.
for entry in "$PUBLISH_DIR"/* "$PUBLISH_DIR"/.[!.]*; do
    [ -e "$entry" ] || continue
    name=$(basename "$entry")
    case "$name" in
        WinPartFlash.app|*.dmg) continue ;;
        com.hcgstudio.winpartflash.helper.plist)
            cp "$entry" "$APP/Contents/Library/LaunchDaemons/"
            continue ;;
    esac
    cp -R "$entry" "$APP/Contents/MacOS/"
done

if [ ! -f "$APP/Contents/MacOS/$EXECUTABLE" ]; then
    echo "Executable '$EXECUTABLE' not found in publish output; check the value passed by MSBuild."
    exit 1
fi
chmod +x "$APP/Contents/MacOS/$EXECUTABLE"
[ -f "$APP/Contents/MacOS/com.hcgstudio.winpartflash.helper" ] && chmod +x "$APP/Contents/MacOS/com.hcgstudio.winpartflash.helper"

# PDBs are .NET debug symbols, not Mach-O.  codesign treats any file next to a
# signed binary as a nested subcomponent and refuses to seal the bundle when
# it finds one that isn't itself signable, so strip them before signing.
find "$APP/Contents/MacOS" -name '*.pdb' -delete

# Materialise Info.plist from the template.
sed -e "s/__EXECUTABLE__/$EXECUTABLE/g" -e "s/__VERSION__/$VERSION/g" \
    "$TEMPLATE" > "$APP/Contents/Info.plist"

# Copy the app icon into Resources so CFBundleIconFile resolves.
ICON_SRC="$SCRIPT_DIR/../Assets/app-icon.icns"
if [ -f "$ICON_SRC" ]; then
    cp "$ICON_SRC" "$APP/Contents/Resources/app-icon.icns"
else
    echo "Warning: app-icon.icns not found at $ICON_SRC" >&2
fi

# Build a read-only compressed dmg.  UDZO is the standard format Finder mounts
# without complaint and is what `notarytool` accepts as a submission payload.
hdiutil create -volname "WinPartFlash" -srcfolder "$APP" -ov -format UDZO "$DMG"

echo "Packaged: $APP"
echo "Image:    $DMG"
