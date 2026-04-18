# WinPartFlash

A cross-platform desktop utility for flashing disk images to individual partitions on Windows, Linux, and macOS. Built with Avalonia/ReactiveUI on .NET 10.

## Features

- Detects disks and partitions on Windows, Linux, and macOS (with direct GPT parsing as a fallback)
- Writes raw or compressed images to a selected partition, or saves a partition back out to an image
- Supports raw, gzip, lz4, xz (decompress), and zstd streams, with tunable compression level and worker count
- Emits/verifies `<image>.sha256` sidecars in a single pass so checksums don't require a re-read
- Ejects the target disk after a successful flash (per-OS backends)
- Built-in Inspect tab (GPT table view, hex peek) and Logging tab (in-app log + copyable system info)
- Privileged block-device access on macOS via a bundled `SMAppService` helper, with `osascript`/`sudo` as a fallback
- Localized UI (English and Simplified Chinese)

## Requirements

- .NET SDK 10.0
- macOS builds additionally require `cmake` to build the bundled native helper library (`WinPartFlashLib`) and the privileged helper (`WinPartFlashHelper`)

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

On macOS the GUI project runs `WinPartFlash.Gui/scripts/macos_build_binary.sh` as part of the build. That script CMake-builds `WinPartFlashLib` and `WinPartFlashHelper`, then copies `libWinPartFlashLib.dylib`, the helper binary, and its launchd plist next to the GUI binary. Publishing to an `osx-*` RID additionally runs `scripts/macos_package.sh` to assemble, sign, and (optionally) notarize an `.app` bundle. To build the native library by hand:

```sh
cmake -S WinPartFlashLib -B WinPartFlashLib/build -DCMAKE_BUILD_TYPE=Release
cmake --build WinPartFlashLib/build
```

## Project Layout

- `WinPartFlash.Gui/` — Avalonia desktop app (`net10.0`, `WinExe`)
  - `PartitionDetector/` — per-OS partition detection and disk ejectors, plus a GPT-based fallback
  - `Compression/` — pluggable stream copiers for raw/gzip/lz4/xz/zstd with level/worker options and ratio probing
  - `MacOS/` — `IPrivilegedDiskGateway` plus `SMAppService` and `osascript` backends, entitlements, and Info.plist template
  - `Inspection/` — GPT reader and hex dump used by the in-app Inspect tab
  - `Logging/`, `Diagnostics/` — in-memory log sink and system-info provider backing the Logging tab
  - `GuidPartition/`, `Utils/` — GPT parsing, CRC, SHA-256 hashing stream, and `.sha256` sidecar helpers
  - `ViewModels/`, `Views/` — ReactiveUI view-models and Avalonia views (main window, partition items, Inspect/Logging tabs, message dialog)
  - `Resources/` — `Strings.resx` plus per-culture overlays
- `WinPartFlashLib/` — C shared library exposing block-device helpers used via P/Invoke on macOS
- `WinPartFlashHelper/` — privileged macOS helper (C, CMake) run via `SMAppService`/launchd for raw block-device access

## License

Released under the [MIT License](LICENSE).
