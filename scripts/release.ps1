param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

# Ensure we're on main branch
$currentBranch = git branch --show-current
if ($currentBranch -ne "main") {
    Write-Error "Must be on main branch (currently on '$currentBranch')"
    exit 1
}

# Ensure working directory is clean
$status = git status --porcelain
if ($status) {
    Write-Error "Working directory is not clean. Commit or stash changes first."
    exit 1
}

# Ensure we're synced with origin/main
git fetch origin main --quiet
$local = git rev-parse HEAD
$remote = git rev-parse origin/main
if ($local -ne $remote) {
    Write-Error "Local main is not synced with origin/main. Pull or push first."
    exit 1
}

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
