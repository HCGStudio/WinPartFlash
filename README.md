# WinPartFlash

A cross-platform desktop utility for flashing disk images to individual partitions on Windows, Linux, and macOS. Built with Avalonia/ReactiveUI on .NET 10.

## Features

- Detects disks and partitions on Windows, Linux, and macOS (with direct GPT parsing as a fallback)
- Writes raw or compressed images to a selected partition
- Supports raw, gzip, lz4, xz (decompress), and zstd streams
- Localized UI (English and Simplified Chinese)

## Requirements

- .NET SDK 10.0
- macOS builds additionally require `cmake` to build the bundled native helper library (`WinPartFlashLib`)

## Build & Run

The solution uses the XML-format `WinPartFlash.slnx` (recent `dotnet` SDKs handle it natively).

```sh
# Build
dotnet build WinPartFlash.slnx

# Run the GUI
dotnet run --project WinPartFlash.Gui

# Publish a single-file, framework-dependent binary
dotnet publish WinPartFlash.Gui -r win-x64     -c Release
dotnet publish WinPartFlash.Gui -r win-arm64   -c Release
dotnet publish WinPartFlash.Gui -r osx-arm64   -c Release
dotnet publish WinPartFlash.Gui -r linux-x64   -c Release
```

On macOS the GUI project runs `WinPartFlash.Gui/scripts/macos_build_binary.sh` as part of the build, which CMake-builds `WinPartFlashLib` and copies `libWinPartFlashLib.dylib` next to the GUI binary. To build the native library by hand:

```sh
cmake -S WinPartFlashLib -B WinPartFlashLib/build -DCMAKE_BUILD_TYPE=Release
cmake --build WinPartFlashLib/build
```

## Project Layout

- `WinPartFlash.Gui/` — Avalonia desktop app (`net10.0`, `WinExe`)
  - `PartitionDetector/` — per-OS partition detection plus a GPT-based fallback
  - `Compression/` — pluggable stream copiers for raw/gzip/lz4/xz/zstd
  - `GuidPartition/`, `Utils/` — GPT parsing and CRC helpers
  - `ViewModels/`, `Views/` — ReactiveUI view-models and Avalonia views
  - `Resources/` — `Strings.resx` plus per-culture overlays
- `WinPartFlashLib/` — C shared library exposing block-device helpers used via P/Invoke on macOS
- `WinPartFlashHelper/` — privileged macOS helper for block-device access

## License

Released under the [MIT License](LICENSE).
