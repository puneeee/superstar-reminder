[CmdletBinding()]
param([string]$InstallRoot = 'D:\SuperstarReminder')
$ErrorActionPreference = 'Stop'
if (-not (Test-Path (Split-Path -Qualifier $InstallRoot))) { throw "The selected drive does not exist. Choose an existing drive with -InstallRoot." }
$tools = Join-Path $InstallRoot 'tools'
$downloads = Join-Path $InstallRoot 'downloads'
$dotnetDir = Join-Path $tools 'dotnet'
New-Item -ItemType Directory -Force $tools,$downloads,$dotnetDir | Out-Null
$script = Join-Path $downloads 'dotnet-install.ps1'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
$previousTemp = $env:TEMP
$previousTmp = $env:TMP
try {
    $env:TEMP = $downloads
    $env:TMP = $downloads
    & $script -Channel '8.0' -InstallDir $dotnetDir -NoPath
    if ($LASTEXITCODE -ne 0) { throw 'The .NET SDK installation failed.' }
} finally {
    $env:TEMP = $previousTemp
    $env:TMP = $previousTmp
}
$env:DOTNET_ROOT = $dotnetDir
$env:PATH = "$dotnetDir;$env:PATH"
$env:DOTNET_CLI_HOME = Join-Path $tools 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $tools 'nuget-packages'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    & "$dotnetDir\dotnet.exe" restore SuperstarReminder.sln
    if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
    & "$dotnetDir\dotnet.exe" build SuperstarReminder.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
    & "$dotnetDir\dotnet.exe" run --project tests/SuperstarReminder.Tests
    if ($LASTEXITCODE -ne 0) { throw 'Scheduling checks failed.' }
    & "$dotnetDir\dotnet.exe" publish src/SuperstarReminder --configuration Release --runtime win-x64 --self-contained true --output "$InstallRoot\app" -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    Write-Host "Application: $InstallRoot\app\SuperstarReminder.exe"
    Write-Host 'Set up local MySQL using docs/SETUP.md, then launch the app and open Settings.'
} finally { Pop-Location }
