$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Join-Path $projectRoot 'artifacts\publish\win-x64'
dotnet publish (Join-Path $projectRoot 'src\SensSync.Desktop\SensSync.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $publishPath
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
$zipPath = Join-Path $projectRoot 'artifacts\SensSync-M1-win-x64.zip'
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath -Force
Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
