#!/bin/sh
# Tolerate missing artefacts so `dotnet clean` is idempotent.
rm -rf ../WinPartFlashLib/build/
rm -rf ../WinPartFlashHelper/build/
rm -f libWinPartFlashLib.dylib
rm -f com.hcgstudio.winpartflash.helper
rm -f com.hcgstudio.winpartflash.helper.plist
# Remove pre-rename artifacts so old files don't linger in builds.
rm -f com.winpartflash.helper com.winpartflash.helper.plist
