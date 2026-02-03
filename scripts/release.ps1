param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Validate version format (4 parts)
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    Write-Error "Version must be in format X.X.X.X (e.g., 1.0.0.0)"
    exit 1
}

# Update manifest
$manifestPath = "$PSScriptRoot/../DisplayBlackout/Package.appxmanifest"
[xml]$manifest = Get-Content $manifestPath
$manifest.Package.Identity.Version = $Version
$manifest.Save($manifestPath)

Write-Host "Updated manifest to version $Version"

# Commit and tag
git add $manifestPath
git commit -m "v$Version"
git tag "v$Version"

Write-Host ""
Write-Host "Created commit and tag v$Version"
Write-Host "Run 'git push && git push --tags' to trigger the release workflow"
