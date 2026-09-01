# Publicerer Skærmlup self-contained (så .NET ikke skal installeres på maskinen) og oversætter
# Inno Setup-installeren. Resultat: installer\out\Skaermlup-Setup-<version>.exe
#
#   pwsh installer\build-skaermlup.ps1
#
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host 'Publicerer Skaermlup.Desktop (Release, self-contained win-x64)…' -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot 'src\Skaermlup.Desktop') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=none
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fejlede ($LASTEXITCODE)" }

$iscc = Get-ChildItem -Path "${env:ProgramFiles}", "${env:ProgramFiles(x86)}" `
    -Filter ISCC.exe -Recurse -Depth 2 -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName

if (-not $iscc) {
    throw 'Inno Setup (ISCC.exe) blev ikke fundet. Installér Inno Setup, og kør scriptet igen.'
}

Write-Host "Oversætter installeren med $iscc…" -ForegroundColor Cyan
& $iscc (Join-Path $PSScriptRoot 'Skaermlup.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC fejlede ($LASTEXITCODE)" }

Write-Host 'Færdig. Installeren ligger i installer\out\.' -ForegroundColor Green
