$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path $PSScriptRoot) 'DisplayBlackout'
function Read-Resources([string]$language) {
    [xml]$xml = Get-Content (Join-Path $project "Strings/$language/Resources.resw") -Raw
    $values = @{}
    foreach ($entry in $xml.root.data) {
        if ($values.ContainsKey($entry.name)) { throw "Duplicate resource: $($entry.name)" }
        if ([string]::IsNullOrWhiteSpace($entry.value)) { throw "Empty resource: $($entry.name)" }
        $values[$entry.name] = [string]$entry.value
    }
    return $values
}
$english = Read-Resources 'en-US'
$chinese = Read-Resources 'zh-CN'
if (Compare-Object @($english.Keys | Sort-Object) @($chinese.Keys | Sort-Object)) {
    throw 'English and Chinese resource keys differ'
}
foreach ($key in $english.Keys) {
    $expected = @([regex]::Matches($english[$key], '\{\d+(?:[^}]*)\}') | ForEach-Object Value | Sort-Object)
    $actual = @([regex]::Matches($chinese[$key], '\{\d+(?:[^}]*)\}') | ForEach-Object Value | Sort-Object)
    if (($expected -join '|') -cne ($actual -join '|')) { throw "Format placeholders differ: $key" }
}
[xml]$manifest = Get-Content (Join-Path $project 'Package.appxmanifest') -Raw
foreach ($language in 'en-US', 'zh-CN') {
    if ($language -notin $manifest.Package.Resources.Resource.Language) { throw "Language not declared: $language" }
}
[xml]$xaml = Get-Content (Join-Path $project 'MainWindow.xaml') -Raw
foreach ($element in $xaml.SelectNodes('//*')) {
    $uid = $element.GetAttribute('Uid', 'http://schemas.microsoft.com/winfx/2006/xaml')
    if ($uid -and -not @($english.Keys | Where-Object { $_.StartsWith($uid + '.', [StringComparison]::Ordinal) }).Count) {
        throw "XAML UID has no resources: $uid"
    }
}
Write-Output "PASS: $($english.Count) bilingual resources; matching keys, placeholders, XAML UIDs and manifest languages."
