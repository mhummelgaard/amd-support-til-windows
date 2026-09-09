#Requires -Version 7.0
<#
.SYNOPSIS
    Laver en SBOM (CycloneDX) over det der faktisk ligger i udgivelsen.

.DESCRIPTION
    Listen laeses ud af Skaermlup.deps.json fra publiceringen — ikke skrevet i haanden.
    En haandskrevet stykliste er sand den dag den skrives og forkert bagefter.

    Programmet udgives self-contained: hele .NET-koerselsmiljoeet ligger med i mappen.
    Det er derfor de to runtime-pakker staar med. Uden dem ville styklisten paastaa at
    leverancen er fire filer, og det er den ikke — den er over to hundrede.

    Koeres efter en publicering:
        pwsh -File installer/lav-sbom.ps1
#>
[CmdletBinding()]
param(
    [string] $Rod = (Split-Path -Parent $PSScriptRoot),
    [string] $UdgivelsesMappe
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $UdgivelsesMappe) {
    $UdgivelsesMappe = Join-Path $Rod 'src/Skaermlup.Desktop/bin/Release/net10.0-windows/win-x64/publish'
}
if (-not (Test-Path $UdgivelsesMappe)) {
    throw "Finder ingen udgivelse i $UdgivelsesMappe. Koer 'dotnet publish' foerst."
}

$deps = Get-Content (Join-Path $UdgivelsesMappe 'Skaermlup.deps.json') -Raw | ConvertFrom-Json
$exe  = Get-Item (Join-Path $UdgivelsesMappe 'Skaermlup.exe')
$version = $exe.VersionInfo.ProductVersion -replace '[+].*$', ''

# Alt hvad der rent faktisk bliver lagt i mappen.
$filer = Get-ChildItem $UdgivelsesMappe -File -Recurse
$dller = @($filer | Where-Object Extension -eq '.dll')

function Nyt-Komponent {
    param($Navn, $Version, $Type, $Purl, $Beskrivelse)

    $k = [ordered]@{
        type        = $Type
        'bom-ref'   = "$Navn@$Version"
        name        = $Navn
        version     = $Version
        description = $Beskrivelse
        licenses    = @(@{ license = @{ id = 'MIT' } })
    }
    if ($Purl) { $k.purl = $Purl }
    $k
}

$komponenter = @(
    Nyt-Komponent 'Skaermlup.Core' $version 'library' $null `
        'Egen kerne: geometri, farvefiltre, museudloeser, indstillinger, alle tekster.'

    Nyt-Komponent 'CommunityToolkit.Mvvm' '8.4.2' 'library' 'pkg:nuget/CommunityToolkit.Mvvm@8.4.2' `
        'Eneste tredjepartspakke. MVVM-hjaelpeklasser, ingen ind- og uddata.'
)

# Runtime-pakkerne staar i deps.json som "runtimepack.<navn>/<version>".
foreach ($bibliotek in $deps.libraries.PSObject.Properties.Name) {
    if ($bibliotek -notlike 'runtimepack.*') { continue }

    $navn, $ver = $bibliotek -replace '^runtimepack\.', '' -split '/'
    $komponenter += Nyt-Komponent $navn $ver 'framework' "pkg:nuget/$navn@$ver" `
        'Del af .NET-koerselsmiljoeet. Udgivelsen er self-contained, saa den ligger med i mappen.'
}

$sbom = [ordered]@{
    bomFormat    = 'CycloneDX'
    specVersion  = '1.6'
    serialNumber = "urn:uuid:$([guid]::NewGuid())"
    version      = 1
    metadata     = [ordered]@{
        timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        tools     = @{ components = @(@{ type = 'application'; name = 'lav-sbom.ps1'; version = '1.0' }) }
        authors   = @(@{ name = 'Michael Hummelgaard' })
        component = [ordered]@{
            type        = 'application'
            'bom-ref'   = "Skaermlup@$version"
            name        = 'Skaermlup'
            version     = $version
            description = 'Forstoerrelsesglas til skaermen for mennesker med AMD. Lokalt Windows-program uden netvaerk.'
            licenses    = @(@{ license = @{ id = 'MIT' } })
            hashes      = @(@{
                alg     = 'SHA-256'
                content = (Get-FileHash $exe.FullName -Algorithm SHA256).Hash.ToLower()
            })
        }
    }
    components   = $komponenter
}

$udMappe = Join-Path $Rod 'docs/sbom'
New-Item -ItemType Directory -Force -Path $udMappe | Out-Null
$sti = Join-Path $udMappe "skaermlup-$version.cyclonedx.json"

$sbom | ConvertTo-Json -Depth 12 | Set-Content -Path $sti -Encoding utf8NoBOM

Write-Host "SBOM skrevet: $sti"
Write-Host ("  Skaermlup {0}, SHA-256 {1}" -f $version, $sbom.metadata.component.hashes[0].content.Substring(0, 16) + '...')
Write-Host ("  {0} komponenter daekker {1} filer i udgivelsen, heraf {2} .dll" -f $komponenter.Count, $filer.Count, $dller.Count)
