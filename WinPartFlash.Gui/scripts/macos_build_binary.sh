#!/bin/sh
set -e

# Build the partition-detection helper library (always needed) and the
# privileged helper binary (macOS, used for on-demand elevation so the GUI
# never has to run under sudo).  Both end up next to the GUI assembly.

cd ../WinPartFlashLib
cmake -S . -B build/ -D CMAKE_BUILD_TYPE=Release
cmake --build build/
cp build/libWinPartFlashLib.dylib ../WinPartFlash.Gui/

cd ../WinPartFlashHelper
cmake -S . -B build/ -D CMAKE_BUILD_TYPE=Release
cmake --build build/
cp build/com.hcgstudio.winpartflash.helper ../WinPartFlash.Gui/
cp com.hcgstudio.winpartflash.helper.plist ../WinPartFlash.Gui/
