param(
    [string]$Repository = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
$Repository = [IO.Path]::GetFullPath($Repository)
$output = Join-Path $Repository 'Licenses'
$packages = @{}
$folders = @{}
foreach ($graph in @('obj\project.assets.json', 'StartupHost\obj\project.assets.json')) {
    $assets = Get-Content -LiteralPath (Join-Path $Repository $graph) -Raw | ConvertFrom-Json
    foreach ($folder in $assets.packageFolders.PSObject.Properties) { $folders[$folder.Name] = $true }
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -eq 'package') { $packages[$library.Name] = $library.Value.path }
    }
    foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
        foreach ($download in $framework.Value.downloadDependencies) {
            $version = ($download.version.Trim('[', ']') -split ',')[0].Trim()
            $packages[$download.name + '/' + $version] = $download.name.ToLowerInvariant() + '/' + $version
        }
    }
}

$documents = [Collections.Generic.List[object]]::new()
$inventory = [Collections.Generic.List[object]]::new()
$terms = [Collections.Generic.List[string]]::new()
$terms.Add("WallpaperSwitch distribution terms`n`nOriginal WallpaperSwitch work is licensed under MIT. Bundled third-party components retain their own copyrights and terms. By installing, you agree to the applicable component terms below, including the Microsoft software license terms for Microsoft components. Those component terms do not replace the MIT license for original WallpaperSwitch work. Full notices are installed in Licenses/ and are available from About > Licenses and notices.`n")
$terms.Add([IO.File]::ReadAllText((Join-Path $Repository 'LICENSE')))
$seenTerms = @{}

function Add-Document([string]$Title, [string]$RelativePath, [string]$Kind) {
    $file = Join-Path $Repository $RelativePath
    if (!(Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing legal document: $RelativePath" }
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    $documents.Add([ordered]@{ title = $Title; path = $RelativePath.Replace('\', '/'); sha256 = $hash; kind = $Kind })
    return $hash
}

$null = Add-Document 'WallpaperSwitch - MIT License' 'LICENSE' 'project'
$null = Add-Document 'Privacy and local data' 'docs/PRIVACY.md' 'privacy'

foreach ($identity in ($packages.Keys | Sort-Object)) {
    $parts = $identity -split '/', 2
    $id = $parts[0]
    $version = $parts[1]
    $package = $null
    foreach ($folder in $folders.Keys) {
        $candidate = Join-Path $folder $packages[$identity]
        if (Test-Path -LiteralPath $candidate -PathType Container) { $package = $candidate; break }
    }
    if (!$package) { throw "Package not restored: $identity" }
    $nuspec = Get-ChildItem -LiteralPath $package -Filter '*.nuspec' -File | Select-Object -First 1
    $metadata = ([xml](Get-Content -LiteralPath $nuspec.FullName -Raw)).package.metadata
    $license = [string]$metadata.license.InnerText
    $buildOnly = $id -match '^Microsoft\.(Windows\.SDK\.|NET\.ILLink\.Tasks|NETCore\.App\.Ref)'
    $files = @(Get-ChildItem -LiteralPath $package -File | Where-Object { $_.Name -match '(?i)^(license|notice|third.party|copying|sdk_license)' })
    if ($metadata.license.type -eq 'file') {
        $declared = Get-Item -LiteralPath (Join-Path $package $license)
        if ($declared.FullName -notin $files.FullName) { $files += $declared }
    }
    if (!($files | Where-Object Name -match '(?i)license|copying')) {
        if ($id -like 'H.*' -and $license -eq 'MIT') {
            $files += Get-Item -LiteralPath (Join-Path $output 'Upstream\H.NotifyIcon-LICENSE.md')
        } elseif ($id -like 'System.*' -and $license -eq 'MIT') {
            $files += Get-Item -LiteralPath (Join-Path $output 'Upstream\dotnet-10-LICENSE.txt')
        } elseif (!$buildOnly) {
            throw "No original license text available for $identity ($license)"
        }
    }
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($file in $files) {
        $relative = 'Licenses/' + $id + '/' + $version + '/' + $file.Name
        $destination = Join-Path $Repository $relative
        [IO.Directory]::CreateDirectory((Split-Path $destination -Parent)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        $hash = Add-Document "$id $version - $($file.Name)" $relative 'third-party'
        $paths.Add($relative)
        if (!$buildOnly -and $file.Name -match '(?i)license|copying' -and !$seenTerms.ContainsKey($hash)) {
            $terms.Add("`n--- $id $version ---`n" + [IO.File]::ReadAllText($file.FullName))
            $seenTerms[$hash] = $true
        }
    }
    $inventory.Add([ordered]@{
        id = $id; version = $version; license = $license; licenseUrl = [string]$metadata.licenseUrl
        source = [string]$metadata.repository.url; sourceCommit = [string]$metadata.repository.commit
        developmentOnly = $buildOnly; documents = @($paths)
    })
}

$null = Add-Document 'Inno Setup - License' 'Licenses/Upstream/Inno-Setup-LICENSE.txt' 'installer'
$null = Add-Document 'Upstream document sources' 'Licenses/Upstream/README.md' 'provenance'
$index = [Collections.Generic.List[string]]::new()
$index.Add("# Third-party licenses and notices`n`nOriginal WallpaperSwitch work uses the MIT license in LICENSE. The packages below retain their own terms. Microsoft binary packages must not be treated as MIT merely because an upstream source repository uses MIT. The inventory includes the app/helper dependency graphs and runtime packs; development-only SDK tools are identified separately. Test-only packages are not bundled.`n`nOriginal legal documents are reproduced in Licenses/ without translation or edits. The installer also presents DISTRIBUTION_TERMS.txt. See docs/LICENSES.md for redistribution guidance.`n")
foreach ($entry in $inventory) {
    $label = if ($entry.license) { $entry.license } else { 'Windows SDK terms (development tool)' }
    $usage = if ($entry.developmentOnly) { 'Development only.' } else { 'Application/helper dependency or runtime pack.' }
    $index.Add("## $($entry.id) $($entry.version)`n`n- Declared license: $label. $usage")
    if ($entry.source) { $index.Add("- Upstream: $($entry.source)") }
    if ($entry.licenseUrl) { $index.Add("- Package license URL: $($entry.licenseUrl)") }
    foreach ($path in $entry.documents) { $index.Add("- [$(Split-Path $path -Leaf)]($path)") }
    $index.Add('')
}
$index.Add("## Installer and translations`n`nThe installer is built with Inno Setup. Its original license is in [Licenses/Upstream/Inno-Setup-LICENSE.txt](Licenses/Upstream/Inno-Setup-LICENSE.txt). The Chinese installer translations retain their upstream author credits; see [installer/Languages/README.md](installer/Languages/README.md).`n")
[IO.File]::WriteAllText((Join-Path $Repository 'THIRD_PARTY_NOTICES.md'), ($index -join "`n"), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $Repository 'DISTRIBUTION_TERMS.txt'), ($terms -join "`n"), [Text.UTF8Encoding]::new($false))
$null = Add-Document 'Third-party licenses and notices - Index' 'THIRD_PARTY_NOTICES.md' 'index'
$null = Add-Document 'Distribution terms' 'DISTRIBUTION_TERMS.txt' 'distribution'
$manifest = [ordered]@{ schemaVersion = 1; documents = @($documents); packages = @($inventory) }
[IO.File]::WriteAllText((Join-Path $output 'manifest.json'), ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Host "Legal inventory: $($inventory.Count) packages, $($documents.Count) documents."
