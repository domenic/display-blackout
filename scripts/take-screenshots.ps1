param(
    [string]$OutputDir = "$PSScriptRoot\..\screenshots",
    [switch]$SkipTrayMenu
)

$ErrorActionPreference = "Stop"

# ─── Prerequisites ────────────────────────────────────────────────────────────
# Before running this script:
# - Create a new virtual desktop (Win+Ctrl+D) and run the script from there.
#   This gives a guaranteed clean desktop with no windows.
# - Ensure the Display Blackout tray icon is pinned to the visible notification area.
# ──────────────────────────────────────────────────────────────────────────────

# ─── P/Invoke ─────────────────────────────────────────────────────────────────

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public static class NativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool SystemParametersInfoW(
        uint uiAction, uint uiParam, string pvParam, uint fWinIni);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(
        IntPtr hwnd, uint dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool SendNotifyMessageW(
        IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    public static extern bool SetProcessDPIAware();

    public const int SW_MINIMIZE = 6;
    public const int SW_RESTORE = 9;

    public const uint SPI_SETDESKWALLPAPER = 0x0014;
    public const uint SPIF_UPDATEINIFILE = 0x01;
    public const uint SPIF_SENDCHANGE = 0x02;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    public const uint DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const byte VK_LWIN = 0x5B;
    public const byte VK_SHIFT = 0x10;
    public const byte VK_B = 0x42;

    public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }
}

// IApplicationActivationManager COM interface for launching packaged (MSIX) apps
[ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IApplicationActivationManager
{
    uint ActivateApplication(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string arguments,
        uint options,
        out uint processId);
}

[ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
public class ApplicationActivationManager {}

public static class AppActivator
{
    public static uint Launch(string aumid, string arguments)
    {
        var mgr = (IApplicationActivationManager)new ApplicationActivationManager();
        const uint AO_NOSPLASHSCREEN = 0x4;
        mgr.ActivateApplication(aumid, arguments, AO_NOSPLASHSCREEN, out uint processId);
        return processId;
    }
}
"@

Add-Type -AssemblyName System.Drawing

# ─── Constants ───────────────────────────────────────────────────────────────

$packageName = "DenicolaTech.DisplayBlackout"
$processName = "DisplayBlackout"
$themePath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize"

# ─── Helper Functions ─────────────────────────────────────────────────────────

function Set-Theme([bool]$Light) {
    $value = $Light ? 1 : 0
    Set-ItemProperty -Path $themePath -Name "AppsUseLightTheme" -Value $value
    Set-ItemProperty -Path $themePath -Name "SystemUsesLightTheme" -Value $value

    # SendNotifyMessage is non-blocking and better suited for broadcasts: it posts
    # to every top-level window's queue without getting stuck on any single window.
    [NativeMethods]::SendNotifyMessageW(
        [NativeMethods]::HWND_BROADCAST,
        [NativeMethods]::WM_SETTINGCHANGE,
        [UIntPtr]::Zero,
        "ImmersiveColorSet"
    ) | Out-Null
}

function Set-Wallpaper([string]$Path) {
    [NativeMethods]::SystemParametersInfoW(
        [NativeMethods]::SPI_SETDESKWALLPAPER,
        0,
        $Path,
        [NativeMethods]::SPIF_UPDATEINIFILE -bor [NativeMethods]::SPIF_SENDCHANGE
    ) | Out-Null
}

function Wait-ForWindow([string]$ProcessName, [int]$TimeoutSeconds = 15) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
            Select-Object -First 1
        if ($proc) {
            return $proc.MainWindowHandle
        }
        Start-Sleep -Milliseconds 250
    }
    throw "Timed out waiting for $ProcessName window"
}

$windowCornerRadius = 12

function Save-WindowScreenshot([IntPtr]$Hwnd, [string]$OutputPath) {
    [NativeMethods]::SetForegroundWindow($Hwnd) | Out-Null
    Start-Sleep -Milliseconds 500

    $rect = New-Object NativeMethods+RECT
    [NativeMethods]::DwmGetWindowAttribute(
        $Hwnd,
        [NativeMethods]::DWMWA_EXTENDED_FRAME_BOUNDS,
        [ref]$rect,
        [System.Runtime.InteropServices.Marshal]::SizeOf($rect)
    ) | Out-Null

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top

    # Capture the on-screen pixels (preserves Mica backdrop colors)
    $bmp = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, (New-Object System.Drawing.Size($width, $height)))
    $g.Dispose()

    # Apply rounded corner transparency by directly adjusting pixel alpha values.
    # This avoids color fringing that occurs with the TextureBrush + FillPath approach,
    # where GDI+ anti-aliasing blends against the background color.
    $r = $windowCornerRadius
    $corners = @(
        @{ CX = $r;            CY = $r },              # top-left
        @{ CX = $width - $r;   CY = $r },              # top-right
        @{ CX = $r;            CY = $height - $r },    # bottom-left
        @{ CX = $width - $r;   CY = $height - $r }     # bottom-right
    )

    foreach ($corner in $corners) {
        $cx = $corner.CX
        $cy = $corner.CY

        # Only process pixels in the corner's bounding box
        $x0 = if ($cx -le $r) { 0 } else { $cx }
        $x1 = if ($cx -le $r) { $r } else { $width }
        $y0 = if ($cy -le $r) { 0 } else { $cy }
        $y1 = if ($cy -le $r) { $r } else { $height }

        for ($py = $y0; $py -lt $y1; $py++) {
            for ($px = $x0; $px -lt $x1; $px++) {
                $dx = $px - $cx + 0.5    # measure from pixel center
                $dy = $py - $cy + 0.5
                $dist = [Math]::Sqrt($dx * $dx + $dy * $dy)

                if ($dist -gt $r + 0.5) {
                    # Fully outside the arc — make transparent
                    $bmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
                } elseif ($dist -gt $r - 0.5) {
                    # Anti-alias: linear falloff over 1 pixel, preserving original RGB
                    $pixel = $bmp.GetPixel($px, $py)
                    $alpha = [int](($r + 0.5 - $dist) * $pixel.A)
                    $bmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb($alpha, $pixel.R, $pixel.G, $pixel.B))
                }
            }
        }
    }

    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    Write-Host "Saved: $OutputPath"
}

function Save-DesktopScreenshot([string]$OutputPath) {
    $allBounds = [System.Drawing.Rectangle]::Empty
    foreach ($screen in [System.Windows.Forms.Screen]::AllScreens) {
        $allBounds = [System.Drawing.Rectangle]::Union($allBounds, $screen.Bounds)
    }

    $bmp = New-Object System.Drawing.Bitmap($allBounds.Width, $allBounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)

    $graphics.CopyFromScreen($allBounds.Left, $allBounds.Top, 0, 0, $allBounds.Size)

    $graphics.Dispose()
    $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    Write-Host "Saved: $OutputPath"
}

function Start-PackagedApp([string]$Arguments) {
    $pkg = Get-AppxPackage -Name $packageName
    if (-not $pkg) {
        throw "Display Blackout is not installed. Run scripts\deploy.ps1 first."
    }
    $manifest = Get-AppxPackageManifest -Package $pkg
    $appId = $manifest.Package.Applications.Application.Id
    $aumid = "$($pkg.PackageFamilyName)!$appId"

    $appPid = [AppActivator]::Launch($aumid, $Arguments)
    Write-Host "Launched app (PID $appPid): $aumid $Arguments"
}

function Send-BlackoutHotkey {
    [NativeMethods]::keybd_event([NativeMethods]::VK_LWIN, 0, 0, [UIntPtr]::Zero)
    [NativeMethods]::keybd_event([NativeMethods]::VK_SHIFT, 0, 0, [UIntPtr]::Zero)
    [NativeMethods]::keybd_event([NativeMethods]::VK_B, 0, 0, [UIntPtr]::Zero)
    [NativeMethods]::keybd_event([NativeMethods]::VK_B, 0, [NativeMethods]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    [NativeMethods]::keybd_event([NativeMethods]::VK_SHIFT, 0, [NativeMethods]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    [NativeMethods]::keybd_event([NativeMethods]::VK_LWIN, 0, [NativeMethods]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
}

# ─── Setup ────────────────────────────────────────────────────────────────────

[NativeMethods]::SetProcessDPIAware() | Out-Null

Add-Type -AssemblyName System.Windows.Forms

$OutputDir = (Resolve-Path $OutputDir).Path

# Save original settings
$origAppsTheme = (Get-ItemProperty -Path $themePath).AppsUseLightTheme
$origSystemTheme = (Get-ItemProperty -Path $themePath).SystemUsesLightTheme
$origWallpaper = (Get-ItemProperty -Path "HKCU:\Control Panel\Desktop").Wallpaper

# Kill any existing app instance
Get-Process -Name $processName -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$pkg = Get-AppxPackage -Name $packageName
if (-not $pkg) {
    throw "Display Blackout is not installed. Run scripts\deploy.ps1 first."
}

try {

# ─── Settings Window Screenshots ─────────────────────────────────────────────

Write-Host ""
Write-Host "Taking settings window screenshots..."

# Use a black wallpaper so Mica backdrop compositing doesn't bleed desktop colors
Set-Wallpaper ""
Start-Sleep -Seconds 1

Set-Theme -Light $true
Start-Sleep -Seconds 5

Start-PackagedApp "/OpenSettings /ResetSettings"

$hwnd = Wait-ForWindow $processName
Write-Host "Settings window found."
Start-Sleep -Seconds 5

Save-WindowScreenshot $hwnd (Join-Path $OutputDir "settings-window-light-mode.png")

# Minimize before theme switch to avoid taskbar flash from window redraw
[NativeMethods]::ShowWindow($hwnd, [NativeMethods]::SW_MINIMIZE) | Out-Null
Set-Theme -Light $false
Start-Sleep -Seconds 5
[NativeMethods]::ShowWindow($hwnd, [NativeMethods]::SW_RESTORE) | Out-Null
Start-Sleep -Seconds 5

Save-WindowScreenshot $hwnd (Join-Path $OutputDir "settings-window-dark-mode.png")

# ─── Multi-Monitor Screenshots ───────────────────────────────────────────────

Write-Host ""
Write-Host "Taking multi-monitor screenshots..."

# Switch to Windows 11 default wallpaper for the desktop screenshots
[NativeMethods]::ShowWindow($hwnd, [NativeMethods]::SW_MINIMIZE) | Out-Null
Set-Wallpaper "C:\Windows\Web\Wallpaper\Windows\img0.jpg"
Set-Theme -Light $true
Start-Sleep -Seconds 5
[NativeMethods]::ShowWindow($hwnd, [NativeMethods]::SW_RESTORE) | Out-Null
Start-Sleep -Milliseconds 500

Save-DesktopScreenshot (Join-Path $OutputDir "3-monitors-off.png")

Send-BlackoutHotkey
Start-Sleep -Seconds 1

Save-DesktopScreenshot (Join-Path $OutputDir "3-monitors-on.png")

Send-BlackoutHotkey
Start-Sleep -Seconds 1

# ─── Tray Menu Screenshot ────────────────────────────────────────────────────

if (-not $SkipTrayMenu) {
    Write-Host ""
    Write-Host "Right-click the Display Blackout tray icon, then press Enter."
    Read-Host
    Start-Sleep -Seconds 1

    $primary = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $captureWidth = 752
    $captureHeight = 701
    $x = $primary.Right - $captureWidth
    $y = $primary.Bottom - $captureHeight

    $bmp = New-Object System.Drawing.Bitmap($captureWidth, $captureHeight)
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    $graphics.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($captureWidth, $captureHeight)))
    $graphics.Dispose()

    $outputPath = Join-Path $OutputDir "system-tray-menu.png"
    $bmp.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    Write-Host "Saved: $outputPath"
}

Write-Host ""
Write-Host "Done! Screenshots saved to: $OutputDir"

} finally {

# ─── Cleanup ──────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Cleaning up..."

Get-Process -Name $processName -ErrorAction SilentlyContinue | Stop-Process -Force

# Restore original theme
Set-ItemProperty -Path $themePath -Name "AppsUseLightTheme" -Value $origAppsTheme
Set-ItemProperty -Path $themePath -Name "SystemUsesLightTheme" -Value $origSystemTheme
$result = [UIntPtr]::Zero
[NativeMethods]::SendMessageTimeoutW(
    [NativeMethods]::HWND_BROADCAST,
    [NativeMethods]::WM_SETTINGCHANGE,
    [UIntPtr]::Zero,
    "ImmersiveColorSet",
    [NativeMethods]::SMTO_ABORTIFHUNG,
    5000,
    [ref]$result
) | Out-Null

# Restore original wallpaper
Set-Wallpaper $origWallpaper

}
