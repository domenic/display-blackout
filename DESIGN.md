# Monitor Blanker - Design Specification

## Overview

**Monitor Blanker** is a Windows 11 system tray utility that blanks (displays opaque black over) selected monitors on demand. Common use cases include reducing distractions while gaming, focusing on a single display, or dimming secondary monitors during video calls.

### Goals

1. **Modern Windows development**: Learn and use the latest Windows App SDK, WinUI 3, and .NET 10 AOT
2. **Frictionless installation**: Zero prerequisites for users via native compilation
3. **Lightweight**: Target ~20-50MB with AOT and trimming
4. **Store distribution**: Publish via Microsoft Store and Winget with proper signing

---

## Technology Stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| **Language** | C# | Modern syntax, excellent Windows ecosystem, async/await |
| **Runtime** | .NET 10 Native AOT | No runtime dependency, ~20-50MB trimmed |
| **UI Framework** | WinUI 3 (Windows App SDK 1.8) | Latest Microsoft UI framework, modern styling |
| **System Tray** | Win32 via CsWin32 or H.NotifyIcon.WinUI | WinUI 3 lacks native tray support |
| **Packaging** | MSIX | Required for Store, auto-updates, clean install/uninstall |
| **Target OS** | Windows 11 (latest builds) | Simplifies APIs, enables modern features |
| **Architectures** | x64, ARM64 | Support both Intel/AMD and Snapdragon/ARM devices |

### Why .NET 10 AOT + WinUI 3?

- **No runtime prompts**: AOT compiles to native code; users don't need .NET installed
- **Modern UI**: WinUI 3 provides Fluent Design, dark mode support, accessibility
- **Maintainable**: C# is more ergonomic than C++/WinRT for this scale of project
- **Future-proof**: This is Microsoft's actively-developed native UI stack

### Size Considerations

WinUI 3 self-contained apps can be 100-200MB. With AOT and IL trimming (supported since Windows App SDK 1.2), we target:

- **Trimmed size**: ~20-50MB (80% reduction from untrimmed)
- **Acceptable trade-off**: Larger than pure Win32 (~5MB) but includes full XAML runtime

---

## Features

### v1.0 - Core Features

#### Blanking Behavior

- **Blank overlay**: Opaque black window covering the entire monitor
  - `WS_EX_TOPMOST` (always on top)
  - `WS_POPUP` (no title bar or borders)
  - `WS_EX_TOOLWINDOW` (hidden from taskbar/Alt+Tab)
  - `WS_EX_NOACTIVATE` (doesn't steal focus)
  - Non-interactive (clicks pass through or are ignored)
- **Default selection**: All monitors except primary
- **Per-monitor control**: User selects which monitors to blank in settings

#### Triggers

| Trigger | Behavior |
|---------|----------|
| **Hotkey** | Default: `Win+Shift+B` (configurable). Toggles blank on/off |
| **System tray double-click** | Toggles blank on/off |
| **System tray single-click** | Opens settings window |
| **Game Mode** | Auto-blank when Windows Game Mode activates; auto-unblank on exit |

#### Settings Window

- **Monitor selector**: Visual layout showing monitor arrangement (like Windows Display Settings). Click monitors to toggle blanking for each
- **Hotkey configuration**: Record a new hotkey combination
- **Game Mode toggle**: Enable/disable automatic blanking during Game Mode
- **Start with Windows**: Toggle auto-start (default: off)

#### System Tray Icon

- **Visual states**:
  - Unblanked: Monitor outline (empty)
  - Blanked: Filled black monitor
- **Interactions**:
  - Single-click: Open settings
  - Double-click: Toggle blank
  - Right-click: Context menu (optional, see below)

### v1.0 - Optional Enhancements

These may be included in v1.0 if time permits:

- **Context menu**: Right-click tray icon for quick actions (Blank All, Settings, Exit)
- **Per-monitor quick toggle**: Context menu lists each monitor for quick individual control
- **Notification**: Toast notification when blanking/unblanking (can be disabled)

### v2.0 - Future Features

- **Process/EXE detection**: Whitelist of executables that trigger auto-blanking
- **Escape hatch**: Safety feature (e.g., triple-click corner, Escape key) to unblank if hotkey fails
- **Profiles**: Named configurations for different monitor setups
- **Scheduling**: Time-based blanking rules
- **Brightness control**: Dim instead of fully black

---

## User Interface Design

### Settings Window

```
┌─────────────────────────────────────────────────────────────┐
│  Monitor Blanker Settings                            ─ □ ✕  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Monitors to blank:                                         │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                                                     │   │
│  │    ┌─────────┐   ┌─────────┐   ┌─────────┐         │   │
│  │    │    1    │   │    2    │   │    3    │         │   │
│  │    │(Primary)│   │  [X]    │   │  [X]    │         │   │
│  │    │         │   │ blanked │   │ blanked │         │   │
│  │    └─────────┘   └─────────┘   └─────────┘         │   │
│  │                                                     │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ─────────────────────────────────────────────────────────  │
│                                                             │
│  Keyboard shortcut:     [ Win + Shift + B ]  [Change...]   │
│                                                             │
│  ─────────────────────────────────────────────────────────  │
│                                                             │
│  ☑ Auto-blank in Game Mode                                  │
│  ☐ Start with Windows                                       │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Tray Icon States

| State | Icon Description |
|-------|------------------|
| Unblanked | Monitor outline, transparent fill |
| Blanked | Monitor outline, solid black fill |

Icons should support:
- Light and dark system themes
- Multiple sizes (16x16, 24x24, 32x32, 48x48)
- High contrast mode accessibility

---

## Technical Implementation

### Project Structure

```
MonitorBlanker/
├── MonitorBlanker.sln
├── src/
│   └── MonitorBlanker/
│       ├── MonitorBlanker.csproj
│       ├── App.xaml(.cs)           # Application entry, tray icon setup
│       ├── MainWindow.xaml(.cs)    # Settings window
│       ├── Services/
│       │   ├── BlankingService.cs  # Creates/manages blank overlays
│       │   ├── HotkeyService.cs    # Global hotkey registration
│       │   ├── GameModeService.cs  # Game Mode detection
│       │   ├── MonitorService.cs   # Monitor enumeration/info
│       │   └── SettingsService.cs  # Persist/load settings
│       ├── ViewModels/
│       │   └── SettingsViewModel.cs
│       ├── Models/
│       │   ├── MonitorInfo.cs
│       │   └── AppSettings.cs
│       ├── Interop/
│       │   └── NativeMethods.txt   # CsWin32 API declarations (see below)
│       └── Assets/
│           └── Icons/              # Tray icons in various sizes
├── Package.appxmanifest
└── README.md
```

### Key Implementation Details

#### 1. Blank Window Creation

Create a borderless black WinUI 3 window positioned on each target monitor:

```csharp
// BlankWindow.xaml - minimal XAML
// <Window ... Title="" xmlns:local="...">
//     <Grid Background="Black" />
// </Window>

// BlankWindow.xaml.cs
public sealed partial class BlankWindow : Window
{
    public BlankWindow(DisplayArea displayArea)
    {
        InitializeComponent();

        // Get native window handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Remove caption, make tool window (hidden from Alt+Tab)
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)(WINDOW_STYLE.WS_CAPTION | WINDOW_STYLE.WS_THICKFRAME);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= (int)(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);

        // Position on target display and make topmost
        var bounds = displayArea.OuterBounds;
        PInvoke.SetWindowPos(
            hwnd,
            HWND.HWND_TOPMOST,
            bounds.X, bounds.Y, bounds.Width, bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
        );
    }
}
```

**Key points:**
- Use `DisplayArea.OuterBounds` for monitor dimensions (includes taskbar area)
- `WS_EX_TOOLWINDOW`: Hidden from Alt+Tab and taskbar
- `WS_EX_NOACTIVATE`: Doesn't steal focus when shown
- `HWND_TOPMOST`: Stays above all normal windows
- `SWP_NOACTIVATE`: Show without activating

#### 2. System Tray (NotifyIcon)

WinUI 3 doesn't have native system tray support. Options:

1. **H.NotifyIcon.WinUI** (NuGet package) - Recommended
   - Well-maintained, WinUI 3-specific
   - Handles context menus, tooltips, balloon notifications

2. **Direct Win32 via CsWin32**
   - `Shell_NotifyIcon` for tray icon management
   - More control, fewer dependencies

#### 3. Global Hotkey Registration

Use CsWin32 for type-safe Win32 interop (add `RegisterHotKey` to `NativeMethods.txt`):

```csharp
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

public class HotkeyService : IDisposable
{
    private const int HOTKEY_ID = 1;
    private HWND _hwnd;

    public void Register(HWND hwnd, HOT_KEY_MODIFIERS modifiers, uint vk)
    {
        _hwnd = hwnd;
        if (!PInvoke.RegisterHotKey(hwnd, HOTKEY_ID, modifiers, vk))
        {
            throw new InvalidOperationException("Failed to register hotkey - may be in use");
        }
    }

    public void Unregister()
    {
        PInvoke.UnregisterHotKey(_hwnd, HOTKEY_ID);
    }

    public void Dispose() => Unregister();
}

// Usage: Register Win+Shift+B
hotkeyService.Register(
    hwnd,
    HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_SHIFT,
    (uint)VIRTUAL_KEY.VK_B
);
```

**Handling WM_HOTKEY**: Use a window subclass or `AppWindow` interop to receive the `WM_HOTKEY` message (value `0x0312`).

#### 4. Game Mode Detection

**Recommended approach** (used by PowerToys) — add `SHQueryUserNotificationState` to `NativeMethods.txt`:

```csharp
using Windows.Win32;
using Windows.Win32.UI.Shell;

public class GameModeService
{
    public bool IsFullScreenGameActive()
    {
        if (PInvoke.SHQueryUserNotificationState(out var state).Succeeded)
        {
            return state == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN;
        }
        return false;
    }
}
```

This detects full-screen Direct3D applications. Simple, reliable, and battle-tested in PowerToys.

**Polling strategy**: Use a `DispatcherTimer` or `PeriodicTimer` checking every 1-2 seconds. Low overhead since it's a single API call.

**Alternative approaches** (if needed for edge cases):
- ETW `ForegroundWindowFullScreen` events for real-time detection
- `GetForegroundWindow()` bounds comparison for non-D3D fullscreen apps

#### 5. Monitor Enumeration

Use the modern `DisplayArea` API from Windows App SDK (not legacy `EnumDisplayMonitors`):

```csharp
using Microsoft.UI.Windowing;

// Get all displays
IReadOnlyList<DisplayArea> displays = DisplayArea.FindAll();

foreach (var display in displays)
{
    // display.DisplayId - unique identifier
    // display.OuterBounds - full monitor area including taskbar
    // display.WorkArea - usable area excluding taskbar
    // display.IsPrimary - whether this is the primary display
}

// Get primary display
DisplayArea primary = DisplayArea.Primary;

// Watch for display changes (connect/disconnect/resolution changes)
DisplayAreaWatcher watcher = DisplayArea.CreateWatcher();
watcher.Added += (sender, args) => { /* monitor connected */ };
watcher.Removed += (sender, args) => { /* monitor disconnected */ };
watcher.Updated += (sender, args) => { /* resolution/position changed */ };
watcher.Start();
```

**Key points:**
- `DisplayArea` is the modern WinUI 3 / Windows App SDK abstraction over `HMONITOR`
- `DisplayAreaWatcher` provides real-time notifications (no need to poll or listen for `WM_DISPLAYCHANGE`)
- Use `OuterBounds` for blank window positioning (covers entire screen including taskbar)

#### 6. Settings Persistence

Store settings in:
- **Packaged app**: `Windows.Storage.ApplicationData.Current.LocalSettings`
- **Alternative**: JSON file in `%LocalAppData%\MonitorBlanker\`

Settings schema:
```json
{
  "blankedMonitors": ["\\\\?\\DISPLAY1", "\\\\?\\DISPLAY2"],
  "hotkey": { "modifiers": ["Win", "Shift"], "key": "B" },
  "autoBlankGameMode": true,
  "startWithWindows": false
}
```

---

## Distribution & Deployment

### Packaging: MSIX

- **Single-project MSIX**: No separate packaging project needed (Windows App SDK 1.0+)
- **Framework-dependent with AOT**: AOT eliminates .NET runtime dependency; Windows App SDK runtime auto-installs via MSIX

### Microsoft Store

1. **Register as developer**: Microsoft Partner Center account (~$19 one-time)
2. **Reserve app name**: "Monitor Blanker"
3. **Submit MSIX package**: Signed with Store certificate
4. **Store listing**: Screenshots, description, privacy policy

### Winget

1. **Create manifest**: YAML file describing the app
2. **Submit PR**: To `microsoft/winget-pkgs` repository
3. **Host installer**: Either link to Store or self-hosted MSIX

### CI/CD with GitHub Actions

Automate builds and publishing using GitHub Actions:

#### Build Workflow

```yaml
# .github/workflows/build.yml
name: Build

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    strategy:
      matrix:
        arch: [x64, arm64]
    steps:
      - uses: actions/checkout@v6

      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.0.x'

      - name: Build
        run: dotnet build src/MonitorBlanker -c Release

      - name: Publish AOT (${{ matrix.arch }})
        run: dotnet publish src/MonitorBlanker -c Release -r win-${{ matrix.arch }} --self-contained

      - name: Upload artifact (${{ matrix.arch }})
        uses: actions/upload-artifact@v4
        with:
          name: MonitorBlanker-${{ matrix.arch }}
          path: src/MonitorBlanker/bin/Release/net10.0-windows*/win-${{ matrix.arch }}/publish/
```

#### Winget Auto-Publish

Use [winget-releaser](https://github.com/vedantmgoyal9/winget-releaser) to automatically submit to Winget on each GitHub Release:

```yaml
# .github/workflows/winget.yml
name: Publish to Winget

on:
  release:
    types: [released]

jobs:
  publish:
    runs-on: windows-latest
    steps:
      - uses: vedantmgoyal9/winget-releaser@main
        with:
          identifier: YourPublisher.MonitorBlanker
          installers-regex: '\.msix$'
          token: ${{ secrets.WINGET_TOKEN }}
```

> **Note**: winget-releaser uses commit-based versioning. Using `@main` ensures latest features. For stability, pin to a specific commit hash and use Dependabot for updates.

**Prerequisites for Winget publishing:**
1. First version must be manually submitted to `microsoft/winget-pkgs`
2. Create a classic GitHub PAT with `public_repo` scope (fine-grained PATs not supported)
3. Fork `microsoft/winget-pkgs` under the same account

### Auto-Start (Start with Windows)

For MSIX packaged apps, use Startup Task:

```xml
<!-- Package.appxmanifest -->
<Extensions>
  <desktop:Extension Category="windows.startupTask">
    <desktop:StartupTask TaskId="MonitorBlankerStartup"
                         Enabled="false"
                         DisplayName="Monitor Blanker" />
  </desktop:Extension>
</Extensions>
```

Controlled via `StartupTask` API at runtime.

---

## Development Environment Setup

### Prerequisites

1. **Visual Studio 2022** (v17.10+, Community edition is free) — **Required**
   - Workload: "Windows application development" (includes Windows App SDK)
   - Individual component: ".NET 10 SDK"
   - Individual component: "Windows 11 SDK (10.0.26100.0)"

   > **Note**: VS Code is not supported for WinUI 3 development. Visual Studio is required for XAML compilation, Hot Reload, and project templates. The `dotnet` CLI alone cannot build WinUI 3 XAML projects.

2. **Windows App SDK 1.8** (included in the workload, or via NuGet)

### Project Configuration

```xml
<!-- MonitorBlanker.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>MSIX</WindowsPackageType>
    <RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>

    <!-- AOT Configuration -->
    <PublishAot>true</PublishAot>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>full</TrimMode>

    <!-- AOT/Trimming analyzers and compatibility -->
    <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
    <EnableAotAnalyzer>true</EnableAotAnalyzer>
    <IsAotCompatible>true</IsAotCompatible>

    <!-- CsWinRT AOT settings (set to 2 to surface warnings for manual review) -->
    <CsWinRTAotWarningLevel>1</CsWinRTAotWarningLevel>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.8.*" />
    <PackageReference Include="H.NotifyIcon.WinUI" Version="2.4.*" />
    <PackageReference Include="Microsoft.Windows.CsWin32" Version="0.3.*">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

### CsWin32 Configuration

Create `NativeMethods.txt` in the project root to declare which Win32 APIs to generate:

```
// NativeMethods.txt - CsWin32 will generate type-safe wrappers for these APIs
RegisterHotKey
UnregisterHotKey
SetWindowPos
GetWindowLong
SetWindowLong
SHQueryUserNotificationState
```

CsWin32 generates code at build time — no runtime reflection, fully AOT-compatible.

### Building

```bash
# Debug build
dotnet build

# Release AOT build (x64)
dotnet publish -c Release -r win-x64 --self-contained

# Release AOT build (ARM64)
dotnet publish -c Release -r win-arm64 --self-contained

# Create MSIX package (via Visual Studio or msbuild)
msbuild /p:Configuration=Release /p:Platform=x64 /p:AppxPackage=true
msbuild /p:Configuration=Release /p:Platform=ARM64 /p:AppxPackage=true
```

For Store/Winget distribution, create an MSIX bundle containing both architectures.

---

## Keyboard Shortcut Research

### Default: `Win+Shift+B`

**Available**: This key combination is not used by Windows 11 or PowerToys.

Similar shortcuts (to avoid conflicts):
- `Ctrl+Win+Shift+B`: GPU driver reset (4 keys, not 3)
- `Win+Shift+S`: Snipping Tool screenshot
- `Win+Shift+C`: PowerToys Color Picker
- `Win+Shift+T`: PowerToys Text Extractor

### Reserved Shortcuts (Cannot Use)

- `Win+L`: Lock screen (Windows reserved)
- `Ctrl+Alt+Del`: Security options (Windows reserved)
- `Win+G`: Xbox Game Bar

---

## Accessibility Considerations

- **High contrast mode**: Icons should remain visible
- **Keyboard navigation**: All settings accessible via Tab/Arrow keys
- **Screen reader**: Proper ARIA labels on controls
- **Escape to close**: Settings window closes on Escape key

---

## Security Considerations

- **No elevation required**: App runs as standard user
- **No network access**: Purely local utility
- **Settings stored locally**: No cloud sync, no telemetry
- **Overlay non-interactive**: Cannot be used to trick users (e.g., fake dialogs)

---

## Testing Plan

### Manual Testing

- [ ] Hotkey toggles blanking on/off
- [ ] Double-click tray icon toggles blanking
- [ ] Single-click tray icon opens settings
- [ ] Monitor selector correctly identifies all monitors
- [ ] Clicking monitor in selector toggles its blank state
- [ ] Game Mode auto-blanks when entering game, unblanks when exiting
- [ ] Hotkey change persists after restart
- [ ] Start with Windows option works
- [ ] Dark mode: UI respects system theme
- [ ] Multi-monitor: Handles monitor connect/disconnect gracefully
- [ ] High DPI: UI scales correctly on 4K displays
- [ ] ARM64: Test on Windows on ARM device (Surface Pro X, Snapdragon laptops)

### Edge Cases

- [ ] All monitors blanked (should still be able to unblank via hotkey)
- [ ] Primary monitor blanked
- [ ] Monitor arrangement changes while blanked
- [ ] Rapid toggle (no race conditions)
- [ ] Game Mode false positives (e.g., fullscreen video)

---

## Open Questions / Future Research

1. **Non-D3D fullscreen apps**: `SHQueryUserNotificationState` only detects D3D fullscreen. May need fallback for Vulkan/OpenGL or windowed-fullscreen games. Test and consider adding `GetForegroundWindow()` bounds check.

2. **Escape hatch**: If users request it, implement triple-click corner or Escape key to unblank without hotkey.

3. **Process detection (v2)**: Use `EnumProcesses` and window enumeration to detect specific executables.

4. **Mica/Acrylic backdrop**: Should settings window use Windows 11's Mica material for modern look?

5. **Localization**: Multi-language support for Store listing and UI.

6. **Microsoft Store auto-publish**: Investigate if Store submission can also be automated via CI (Partner Center API).

---

## References

### Documentation

- [Windows App SDK Overview](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [WinUI 3 Documentation](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
- [.NET Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [System Tray (NotifyIcon) in WinUI 3](https://albertakhmetov.com/posts/2025/using-notifyicon-in-winui-3/)
- [H.NotifyIcon.WinUI](https://github.com/HavenDV/H.NotifyIcon)
- [CsWin32 Source Generator](https://github.com/microsoft/CsWin32)
- [Dark Mode in Win32 Apps](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-windows-themes)
- [Game Mode API](https://learn.microsoft.com/en-us/windows/win32/api/_gamemode/)
- [PowerToys Game Mode Detection](https://github.com/microsoft/PowerToys/blob/main/src/common/utils/game_mode.h) - Simple `SHQueryUserNotificationState` approach
- [MSIX Packaging Overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/)
- [winget-releaser GitHub Action](https://github.com/vedantmgoyal9/winget-releaser) - Auto-publish to Winget
- [DisplayArea Class](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.displayarea) - Modern monitor enumeration
- [CsWinRT AOT/Trimming Guide](https://github.com/microsoft/CsWinRT/blob/master/docs/aot-trimming.md) - AOT compatibility for WinRT projections

### PowerToys Shortcuts Reference

- [PowerToys Keyboard Shortcuts](https://defkey.com/microsoft-powertoys-2022-shortcuts)

---

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2026-01-04 | 0.1 | Initial design specification |
| 2026-01-04 | 0.2 | Modernity pass: Updated GitHub Actions to v5/v6, winget-releaser to @main, modernized blank window creation with AppWindow/SetWindowPos, replaced EnumDisplayMonitors with DisplayArea/DisplayAreaWatcher, updated hotkey/game mode code to use CsWin32, added AOT analyzers and CsWinRT settings, added NativeMethods.txt config |
