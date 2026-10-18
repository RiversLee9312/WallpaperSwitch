param(
    [string]$NuGetSource,
    [string]$InnoCompiler = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $repository 'artifacts\publish-1.3.1'
$restore = @('-p:NuGetAudit=false')
if ($NuGetSource) {
    if (Test-Path -LiteralPath $NuGetSource) { $NuGetSource = (Resolve-Path -LiteralPath $NuGetSource).Path }
    $restore += @('--source', $NuGetSource)
}
Push-Location $repository
try {
    & dotnet restore WallpaperSwitch.csproj -r win-x64 -p:SelfContained=true @restore
    if ($LASTEXITCODE) { throw 'Application restore failed.' }
    & dotnet publish WallpaperSwitch.csproj --no-restore -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $publish
    if ($LASTEXITCODE) { throw 'Application publish failed.' }
    & dotnet restore StartupHost\WallpaperSwitch.StartupHost.csproj @restore
    if ($LASTEXITCODE) { throw 'Startup service restore failed.' }
    & dotnet publish StartupHost\WallpaperSwitch.StartupHost.csproj --no-restore -c Release -o (Join-Path $publish 'StartupService') -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE) { throw 'Startup service publish failed.' }
    & dotnet restore Tests\WallpaperSwitch.Tests.csproj @restore
    if ($LASTEXITCODE) { throw 'MSTest restore failed.' }
    & dotnet build Tests\WallpaperSwitch.Tests.csproj --no-restore -c Release
    if ($LASTEXITCODE) { throw 'MSTest build failed.' }
    & (Join-Path $repository 'Tests\bin\x64\Release\net8.0-windows\WallpaperSwitch.Tests.exe') --filter 'TestCategory!=UI&TestCategory!=ServiceIntegration' --no-ansi --report-trx --results-directory (Join-Path $repository 'artifacts\test-results\unit')
    if ($LASTEXITCODE) { throw 'MSTest unit tests failed.' }
    if (!(Test-Path -LiteralPath $InnoCompiler)) { throw "Inno Setup compiler not found: $InnoCompiler" }
    & $InnoCompiler "/DPublishDir=$publish" installer\WallpaperSwitch.iss
    if ($LASTEXITCODE) { throw 'Installer compilation failed.' }
} finally { Pop-Location }
