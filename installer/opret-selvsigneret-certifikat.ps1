# Opretter et selvsigneret kodesigneringscertifikat, så Skærmlup kan bygges med uiAccess og
# dermed ligge oven på Start-menuen og resten af Windows' egen skal.
#
#   pwsh installer\opret-selvsigneret-certifikat.ps1
#
# LÆS DETTE FØRST
#
# Scriptet lægger et nyt rodcertifikat ind i maskinens liste over betroede udstedere. Det
# betyder, at Windows fremover stoler på alt hvad der er signeret med netop dét certifikat.
# Så længe den private nøgle bliver på maskinen og ikke deles, er det uproblematisk — men
# kommer nøglen i de forkerte hænder, kan der signeres programmer som maskinen vil stole på.
#
# Brug det derfor kun på maskiner du selv har hånd om. Skal Skærmlup deles med andre, er det
# et rigtigt kodesigneringscertifikat fra en certifikatudsteder der skal bruges — så slipper
# modtagerne også for SmartScreen-advarslen.
#
# Scriptet skal køres som administrator, fordi det skriver til maskinens certifikatlager.
[CmdletBinding()]
param(
    [string]$Subject = 'CN=Skaermlup (lokal signering)',
    [int]$Years = 5
)

$ErrorActionPreference = 'Stop'

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).
    IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw 'Kør scriptet som administrator: det skal skrive til maskinens certifikatlager.' }

Write-Host "Opretter certifikat: $Subject" -ForegroundColor Cyan
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -CertStoreLocation Cert:\CurrentUser\My `
    -NotAfter (Get-Date).AddYears($Years) `
    -KeyUsage DigitalSignature `
    -KeyExportPolicy NonExportable

Write-Host 'Lægger certifikatet ind som betroet udsteder…' -ForegroundColor Cyan
$temp = Join-Path $env:TEMP 'skaermlup-signering.cer'
Export-Certificate -Cert $cert -FilePath $temp | Out-Null
Import-Certificate -FilePath $temp -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
Import-Certificate -FilePath $temp -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
Remove-Item $temp -Force

Write-Host ''
Write-Host 'Færdig. Byg nu installeren med:' -ForegroundColor Green
Write-Host "  pwsh installer\build-installer.ps1 -CertificateThumbprint $($cert.Thumbprint)" -ForegroundColor Green
Write-Host ''
Write-Host 'Fortryder du, fjernes certifikatet igen med:' -ForegroundColor Yellow
Write-Host "  Get-ChildItem Cert:\LocalMachine\Root, Cert:\LocalMachine\TrustedPublisher, Cert:\CurrentUser\My |" -ForegroundColor Yellow
Write-Host "    Where-Object Thumbprint -eq $($cert.Thumbprint) | Remove-Item" -ForegroundColor Yellow
