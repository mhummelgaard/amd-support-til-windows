# Publicerer Skærmlup self-contained (så .NET ikke skal installeres på maskinen) og oversætter
# Inno Setup-installeren. Resultat: installer\out\Skaermlup-Setup-<version>.exe
#
#   pwsh installer\build-installer.ps1
#
# Med et kodesigneringscertifikat kan luppen desuden få lov at ligge oven på Start-menuen og
# resten af Windows' egen skal. Det kræver uiAccess, som Windows kun tillader for signerede
# programmer installeret under Programmer:
#
#   pwsh installer\build-installer.ps1 -CertificateThumbprint <aftryk>
#
# Uden signering starter en uiAccess-udgave slet ikke, så de to ting hænger sammen og
# slås til under ét.
[CmdletBinding()]
param(
    # Aftrykket på et kodesigneringscertifikat i Cert:\CurrentUser\My.
    [string]$CertificateThumbprint,

    # Tidsstempeltjeneste, så signaturen holder efter certifikatet udløber.
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$signing = -not [string]::IsNullOrWhiteSpace($CertificateThumbprint)

function Find-Tool([string]$name) {
    Get-ChildItem -Path "${env:ProgramFiles}", "${env:ProgramFiles(x86)}" `
        -Filter $name -Recurse -Depth 4 -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}

function Invoke-Signing([string]$path) {
    if (-not $signing) { return }

    $signtool = Find-Tool 'signtool.exe'
    if (-not $signtool) { throw 'signtool.exe blev ikke fundet. Installér Windows SDK, og kør scriptet igen.' }

    & $signtool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $path
    if ($LASTEXITCODE -ne 0) { throw "Signering af $path fejlede ($LASTEXITCODE)" }
}

if ($signing) {
    Write-Host 'Bygger med uiAccess: luppen maa ligge oven paa Start-menuen.' -ForegroundColor Cyan
} else {
    Write-Host 'Bygger uden signering. Luppen gaar ind bag Start-menuen og andre' -ForegroundColor Yellow
    Write-Host 'Windows-flader; det kraever et kodesigneringscertifikat at undgaa.' -ForegroundColor Yellow
}

Write-Host 'Publicerer Skaermlup.Desktop (Release, self-contained win-x64)…' -ForegroundColor Cyan
dotnet publish (Join-Path $repoRoot 'src\Skaermlup.Desktop') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=none -p:UiAccess=$signing
if ($LASTEXITCODE -ne 0) { throw "dotnet publish fejlede ($LASTEXITCODE)" }

$publishDir = Join-Path $repoRoot 'src\Skaermlup.Desktop\bin\Release\net10.0-windows\win-x64\publish'
Invoke-Signing (Join-Path $publishDir 'Skaermlup.exe')

$iscc = Find-Tool 'ISCC.exe'
if (-not $iscc) { throw 'Inno Setup (ISCC.exe) blev ikke fundet. Installér Inno Setup, og kør scriptet igen.' }

Write-Host "Oversætter installeren med $iscc…" -ForegroundColor Cyan
& $iscc (Join-Path $PSScriptRoot 'Skaermlup.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC fejlede ($LASTEXITCODE)" }

$setup = Get-ChildItem (Join-Path $PSScriptRoot 'out') -Filter 'Skaermlup-Setup-*.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($setup) { Invoke-Signing $setup.FullName }

Write-Host 'Færdig. Installeren ligger i installer\out\.' -ForegroundColor Green
