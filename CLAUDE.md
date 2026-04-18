# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

- Solution file is the XML-format `WinPartFlash.slnx` (no legacy `.sln`); recent `dotnet` SDKs handle it natively.
- Build/run GUI: `dotnet build WinPartFlash.slnx` / `dotnet run --project WinPartFlash.Gui`
- Publish (single-file, framework-dependent): `dotnet publish WinPartFlash.Gui -r <win-x64|win-arm64|osx-arm64|linux-x64> -c Release`
- On macOS the GUI csproj has a `BuildBinMac` target that runs `WinPartFlash.Gui/scripts/macos_build_binary.sh`, which CMake-builds `WinPartFlashLib` plus the `WinPartFlashHelper` privileged helper and copies `libWinPartFlashLib.dylib`, `com.hcgstudio.winpartflash.helper`, and its plist next to the GUI binary. Building on macOS therefore requires `cmake`. A separate `PackageMac` target runs after `Publish` (for `osx-*` RIDs) and invokes `scripts/macos_package.sh` to assemble, sign, and notarize an `.app` bundle.
- Build the native lib by hand: `cmake -S WinPartFlashLib -B WinPartFlashLib/build -DCMAKE_BUILD_TYPE=Release && cmake --build WinPartFlashLib/build`
- There is no test project in the solution. `WinPartFlashLib/test.c` builds as a standalone CMake executable (`WinPartFlashLibTest`) and is not run by `dotnet test`.

## Architecture

Three-project layout: an Avalonia/ReactiveUI desktop GUI (`WinPartFlash.Gui`, `net10.0`, `WinExe`); a small C shared library (`WinPartFlashLib`) exposing platform-specific block-device helpers (currently `getBlockSize`) consumed via P/Invoke; and a privileged macOS helper binary (`WinPartFlashHelper`, C, built via CMake) registered through `SMAppService` that performs raw block-device I/O as root on behalf of the sandboxed GUI. The native lib and the helper only ship on macOS builds; Windows/Linux paths use OS-native APIs directly.

Composition happens in `App.OnFrameworkInitializationCompleted` (`WinPartFlash.Gui/App.axaml.cs`): a `Microsoft.Extensions.DependencyInjection` container is built from a set of `Add*` extension methods (`AddLogging`, `AddMacOSPrivileges`, `AddPartitionDetector`, `AddCompressionStreamCopier`, `AddLogSink`, `AddDiagnostics`, `AddViewsAndViewModels`, `AddFileOpenHelper`) and exposed as `App.ServiceProvider`. The `MainWindow` is resolved from a DI scope rather than `new`'d. When adding a new service/view/viewmodel, register it in the matching `*Extensions.cs` file rather than instantiating directly.

Key subsystems, each behind an interface and selected/registered via its extension class:

- `PartitionDetector/` — `IPartitionDetector` with per-OS implementations (`WindowsPartitionDetector`, `LinuxPartitionDetector`, `MacOSPartitionDetector`) plus `GuidPartitionTableBasedPartitionDetector` for parsing GPTs directly, and `IDiskEjector` with per-OS implementations (`WindowsDiskEjector`, `LinuxDiskEjector`, `MacOSDiskEjector`). `PartitionScanOptions` controls scan behavior. macOS implementation P/Invokes `libWinPartFlashLib` for block sizes and uses the privileged gateway (below) for raw reads.
- `MacOS/` — `IPrivilegedDiskGateway` abstracts root-owned block-device I/O; `MacOSPrivilegedDiskGatewayFactory` picks between `SmAppServicePrivilegedDiskGateway` (preferred, via the bundled `WinPartFlashHelper` + `SMAppService`/launchd) and `OsascriptPrivilegedDiskGateway` (fallback via `osascript`-prompted `sudo`). `entitlements.*.plist`, `Info.plist.template`, and `PrivilegedExceptions.cs` support bundling/sandboxing. `AddMacOSPrivileges` is a no-op on non-macOS platforms so DI composition stays portable.
- `Compression/` — `ICompressionStreamCopier` with one implementation per format (raw, gzip, lz4, xz-decompress-only, zstd) selected through `ICompressionStreamCopierFactory` keyed on `CompressionType`. `CompressionOptions` (level, worker count) and `CompressionLevelInfo` expose tunables to the UI; `CompressionRatioProbe` estimates achievable ratio on a sample. xz is decompress-only by design (no compress copier exists).
- `GuidPartition/` + `Utils/GuidPartitionTableHelper.cs`, `Utils/Crc32.cs`, `Utils/SubStream.cs` — pure GPT parsing/CRC helpers used by the GPT-based detector and the Inspect tab.
- `Inspection/` — `GuidPartitionTableReader` and `HexDump` power the in-app Inspect tab (live GPT view plus hex peek of arbitrary offsets).
- `Logging/` — `ILogSink` (`LogSink`) exposes an `ObservableCollection<LogEntry>` consumed by the Logging tab; register via `AddLogSink`. `Microsoft.Extensions.Logging` providers (console) are added alongside.
- `Diagnostics/` — `ISystemInfoProvider` / `SystemInfoSnapshot` produce a one-shot system-info blob for the Logging tab (copy-to-clipboard for bug reports).
- `Utils/HashingStream`, `Utils/ChecksumSidecar`, `Utils/StreamCopyHelper` — pass-through SHA-256 stream wrapper plus reader/writer for GNU-sha256sum-format `<image>.sha256` sidecars. Used so a flash or save emits a checksum in a single pass.
- `FileOpenHelper/` — abstraction over native open dialogs.
- `Views/MessageDialog.cs` — Avalonia-native info/confirm dialog used in place of `MessageBoxManager` so dialog chrome/localization stays consistent.
- `ViewModels/` use ReactiveUI; views are wired via `ViewLocator.cs` and Avalonia compiled bindings (`AvaloniaUseCompiledBindingsByDefault=true`). The main window hosts partition-picking plus `InspectTab` and `LoggingTab` as sibling tabs.

Localization: user-facing strings live in `Resources/Strings.resx` with per-culture overlays at `Resources/Strings.{culture}.resx` (e.g. `Strings.zh-hans.resx`). The strongly-typed accessor is `Strings.Designer.cs` — when adding a key, update the designer alongside the resx (the file is checked in, not regenerated automatically by `dotnet build`). `App` sets `Strings.Culture = CultureInfo.CurrentUICulture` at startup. Bind from XAML with `{x:Static lang:Strings.X}` (the `lang:` prefix is `xmlns:lang="clr-namespace:WinPartFlash.Gui.Resources"`); reference from C# as `Strings.X`. Don't hardcode user-facing text — that includes section labels, native menu items, tab headers, and dialog titles passed to `MessageDialog`/`StorageProvider`.

Unsafe code is enabled (`AllowUnsafeBlocks`) for the partition/GPT parsing paths.
