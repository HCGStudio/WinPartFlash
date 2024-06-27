#!/bin/sh

cd ../WinPartFlashLib || exit 1
cmake -S . -B build/ -D CMAKE_BUILD_TYPE=Release
cmake --build build/
cp build/libWinPartFlashLib.dylib ../WinPartFlash.Gui/
