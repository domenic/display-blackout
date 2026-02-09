param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Split-Path $PSScriptRoot
$projectFile = Get-ChildItem $repoRoot -Filter "*.csproj" -Recurse | Select-Object -First 1
if (-not $projectFile) {
    throw "No .csproj found under $repoRoot"
}
$projectName = $projectFile.BaseName

# ─── Build ────────────────────────────────────────────────────────────────────

Write-Host "Building ($Configuration|$Platform)..."
dotnet build $projectFile.FullName -c $Configuration -p:Platform=$Platform

# ─── Parse the .build.appxrecipe ──────────────────────────────────────────────

$recipeFile = Get-ChildItem (Join-Path $repoRoot "$projectName\bin\$Platform\$Configuration") `
    -Filter "*.build.appxrecipe" -Recurse | Select-Object -First 1
if (-not $recipeFile) {
    throw "No .build.appxrecipe found under $projectName\bin\$Platform\$Configuration"
}
$recipePath = $recipeFile.FullName

[xml]$recipe = Get-Content $recipePath
$ns = @{ ms = "http://schemas.microsoft.com/developer/msbuild/2003" }
$layoutDir = (Select-Xml -Xml $recipe -XPath "//ms:LayoutDir" -Namespace $ns).Node.InnerText

Write-Host "Layout directory: $layoutDir"

# ─── Kill any running instance ────────────────────────────────────────────────

$proc = Get-Process -Name $projectName -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "Stopping running instance..."
    $proc | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# ─── Copy files to AppX layout ───────────────────────────────────────────────

Write-Host "Deploying to layout..."

# Manifest
$manifestNodes = Select-Xml -Xml $recipe -XPath "//ms:AppXManifest" -Namespace $ns
foreach ($node in $manifestNodes) {
    $src = $node.Node.GetAttribute("Include")
    $rel = (Select-Xml -Xml $node.Node -XPath "ms:PackagePath" -Namespace $ns).Node.InnerText
    $dst = Join-Path $layoutDir $rel
    $dstDir = Split-Path $dst
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    Copy-Item -Path $src -Destination $dst -Force
}

# All packaged files
$fileNodes = Select-Xml -Xml $recipe -XPath "//ms:AppxPackagedFile" -Namespace $ns
$count = 0
foreach ($node in $fileNodes) {
    $src = $node.Node.GetAttribute("Include")
    $rel = (Select-Xml -Xml $node.Node -XPath "ms:PackagePath" -Namespace $ns).Node.InnerText
    $dst = Join-Path $layoutDir $rel
    $dstDir = Split-Path $dst
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $dst -Force
        $count++
    } else {
        Write-Warning "Source not found: $src"
    }
}

Write-Host "Copied $count files."

# ─── Register the package ─────────────────────────────────────────────────────

# For a dev-mode package that's already registered at this LayoutDir, the file
# copy above is sufficient - the next launch will pick up the new binaries.
# We still attempt re-registration in case this is a first deploy or the
# manifest changed, but ignore "already installed" errors.
$manifest = Join-Path $layoutDir "AppxManifest.xml"
Write-Host "Registering package..."
try {
    Add-AppxPackage -Register $manifest -ForceApplicationShutdown
} catch {
    if ($_.Exception.Message -match "0x80073CFB|already installed") {
        Write-Host "(Package already registered - updated files in place.)"
    } else {
        throw
    }
}

Write-Host "Done!"
