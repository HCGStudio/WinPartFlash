# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

- Solution file is the XML-format `WinPartFlash.slnx` (no legacy `.sln`); recent `dotnet` SDKs handle it natively.
- Build/run GUI: `dotnet build WinPartFlash.slnx` / `dotnet run --project WinPartFlash.Gui`
- Publish (single-file, framework-dependent): `dotnet publish WinPartFlash.Gui -r <win-x64|win-arm64|osx-arm64> -c Release`
- On macOS the GUI csproj has a `BuildBinMac` target that runs `WinPartFlash.Gui/scripts/macos_build_binary.sh`, which CMake-builds `WinPartFlashLib` and copies `libWinPartFlashLib.dylib` next to the GUI binary. Building on macOS therefore requires `cmake`.
- Build the native lib by hand: `cmake -S WinPartFlashLib -B WinPartFlashLib/build -DCMAKE_BUILD_TYPE=Release && cmake --build WinPartFlashLib/build`
- There is no test project in the solution. `WinPartFlashLib/test.c` builds as a standalone CMake executable (`WinPartFlashLibTest`) and is not run by `dotnet test`.

## Architecture

Two-project solution: an Avalonia/ReactiveUI desktop GUI (`WinPartFlash.Gui`, `net10.0`, `WinExe`) plus a small C shared library (`WinPartFlashLib`) that exposes platform-specific block-device helpers (currently `getBlockSize`) consumed via P/Invoke. The native lib only ships on macOS builds; Windows/Linux paths use OS-native APIs directly.

Composition happens in `App.OnFrameworkInitializationCompleted` (`WinPartFlash.Gui/App.axaml.cs`): a `Microsoft.Extensions.DependencyInjection` container is built from a set of `Add*` extension methods (`AddPartitionDetector`, `AddCompressionStreamCopier`, `AddViewsAndViewModels`, `AddFileOpenHelper`) and exposed as `App.ServiceProvider`. The `MainWindow` is resolved from DI rather than `new`'d. When adding a new service/view/viewmodel, register it in the matching `*Extensions.cs` file rather than instantiating directly.

Key subsystems, each behind an interface and selected/registered via its extension class:

- `PartitionDetector/` — `IPartitionDetecter` with per-OS implementations (`WindowsPartitionDetector`, `LinuxPartitionDetector`, `MacOSPartitionDetector`) plus `GuidPartitionTableBasedPartitionDetector` for parsing GPTs directly. macOS implementation P/Invokes `libWinPartFlashLib` for block sizes.
- `Compression/` — `ICompressionStreamCopier` with one implementation per format (raw, gzip, lz4, xz-decompress-only, zstd) selected through `ICompressionStreamCopierFactory` keyed on `CompressionType`. xz is decompress-only by design (no compress copier exists).
- `GuidPartition/` + `Utils/GuidPartitionTableHelper.cs`, `Utils/Crc32.cs`, `Utils/SubStream.cs` — pure GPT parsing/CRC helpers used by the GPT-based detector.
- `FileOpenHelper/` — abstraction over native open dialogs.
- `ViewModels/` use ReactiveUI; views are wired via `ViewLocator.cs` and Avalonia compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`).

Localization: user-facing strings live in `Resources/Strings.resx` (designer-generated `Strings.Designer.cs`). `App` sets `Strings.Culture = CultureInfo.CurrentUICulture` at startup — add new strings via the resx, don't hardcode.

Unsafe code is enabled (`AllowUnsafeBlocks`) for the partition/GPT parsing paths.
