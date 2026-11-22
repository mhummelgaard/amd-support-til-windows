#Requires -Version 7.0
<#
.SYNOPSIS
    Efterproever paastandene i docs/dataflow-og-compliance.md mod kildekoden.

.DESCRIPTION
    Dokumentet paastaar en raekke ting om hvad Skaermlup ikke goer. Her bliver de
    paastande til noget der kan fejle. Aendrer koden sig saa en paastand falder,
    stopper scriptet med kode 1 — og saa skal dokumentet rettes i samme commit.

    Kores uden argumenter fra roden af arkivet:  pwsh -File docs/efterproev-dataflow.ps1
#>
[CmdletBinding()]
param(
    [string] $Rod = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$kilde = Join-Path $Rod 'src'
if (-not (Test-Path $kilde)) { throw "Finder ikke 'src' under $Rod. Angiv -Rod." }

$fejl = 0

function Test-Paastand {
    param([string] $Navn, [scriptblock] $Proeve)

    $fund = & $Proeve
    if ($fund) {
        $script:fejl++
        Write-Host "FEJL  $Navn" -ForegroundColor Red
        $fund | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkRed }
    }
    else {
        Write-Host "ok    $Navn" -ForegroundColor Green
    }
}

# Kun det udgivne program taeller. Testprojektet maa gerne bruge hvad det vil.
$cs = Get-ChildItem $kilde -Recurse -Include *.cs, *.xaml |
      Where-Object { $d = $_.FullName.Split([IO.Path]::DirectorySeparatorChar); ($d -notcontains 'obj') -and ($d -notcontains 'bin') }

Write-Host "Efterproever $($cs.Count) filer under $kilde`n"

Test-Paastand 'Ingen netvaerks-API i programmets egen kode' {
    $moenster = 'HttpClient|WebClient|WebRequest|\bSocket\b|System\.Net|Dns\.|TcpClient|' +
                'UdpClient|SmtpClient|WebSocket|HttpListener|FtpWebRequest'
    $cs | Select-String -Pattern $moenster | ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'Ingen tastaturkrog' {
    $cs | Select-String -Pattern 'WH_KEYBOARD' | ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'Ingen udklipsholder' {
    $cs | Select-String -Pattern '\bClipboard\b' | ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'GetAsyncKeyState spoerges kun om Ctrl og Skift' {
    $cs | Select-String -Pattern 'GetAsyncKeyState\(' |
        Where-Object { $_.Line -notmatch 'VK_CONTROL|VK_SHIFT|int virtualKey|extern|DllImport' } |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'Native kald kun mod user32, gdi32 og kernel32' {
    $cs | Select-String -Pattern 'DllImport\("([^"]+)"' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
        Where-Object { $_ -notmatch '^(user32|gdi32|kernel32)\.dll$' } |
        Sort-Object -Unique
}

Test-Paastand 'Kun CommunityToolkit.Mvvm som afhaengighed i det udgivne program' {
    Get-ChildItem $kilde -Recurse -Filter *.csproj |
        Select-String -Pattern 'PackageReference Include="([^"]+)"' -AllMatches |
        ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } |
        Where-Object { $_ -ne 'CommunityToolkit.Mvvm' } |
        Sort-Object -Unique
}

Test-Paastand 'Programmet beder ikke om forhoejede rettigheder' {
    Get-ChildItem $kilde -Recurse -Filter app*.manifest |
        Select-String -Pattern 'requestedExecutionLevel' |
        Where-Object { $_.Line -notmatch 'asInvoker' } |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'Skrivning til disk kun fra de kendte steder' {
    $kendte = 'SettingsStore.cs', 'SystemCursor.cs', 'AppShell.cs', 'DiagnosticImages.cs', 'SampleRequest.cs'
    $cs | Select-String -Pattern 'File\.(WriteAll|Create|Move|AppendAll)|Directory\.CreateDirectory' |
        Where-Object { $_.Filename -notin $kendte } |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'Registreringsdatabasen roeres kun i brugerens egen gren — undtagen IT-politikken (D12)' {
    $cs | Select-String -Pattern 'Registry\.|RegistryKey\.OpenBaseKey|RegistryHive' |
        Where-Object { $_.Line -notmatch 'Registry\.CurrentUser|RegistryValueKind' } |
        Where-Object { -not ($_.Filename -eq 'MachinePolicyReader.cs' -and $_.Line -match 'Registry\.LocalMachine\.OpenSubKey\(MachinePolicy\.RegistryPath\)') } |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'IT-politikken under HKLM laeses kun — der skrives aldrig (D12)' {
    $cs | Where-Object Name -eq 'MachinePolicyReader.cs' |
        Select-String -Pattern 'CreateSubKey|SetValue|DeleteValue|DeleteSubKey|SetAccessControl|writable|OpenSubKey\([^)]*,' |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'Proevebilleder gemmes kun eet sted, og foerst efter IT-politikken er spurgt (D10, D12)' {
    $kald = @($cs | Select-String -Pattern 'DiagnosticImages\.SaveTo\(')
    $kald | Where-Object Filename -ne 'AppShell.cs' |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  kalder DiagnosticImages.SaveTo uden for AppShell" }

    foreach ($k in $kald | Where-Object Filename -eq 'AppShell.cs') {
        $linjer = Get-Content $k.Path
        $foer = $linjer[[math]::Max(0, $k.LineNumber - 4)..($k.LineNumber - 2)] -join "`n"
        if ($foer -notmatch 'MachinePolicyReader\.Read\(\)\.SampleImagesBlocked') {
            "AppShell.cs:$($k.LineNumber)  DiagnosticImages.SaveTo uden at spoerge MachinePolicyReader lige foer"
        }
    }

    if ($kald.Count -eq 0) { 'DiagnosticImages.SaveTo blev ikke fundet — er paastanden stadig rigtig formuleret?' }
}

Test-Paastand 'Gruppepolitikskabelonen skriver netop der, hvor programmet laeser (D12)' {
    $mappe = Join-Path $Rod 'installer/gruppepolitik'
    $admxFil = Join-Path $mappe 'Skaermlup.admx'
    if (-not (Test-Path $admxFil)) { return 'installer/gruppepolitik/Skaermlup.admx mangler' }

    $policy = Get-Content -Raw (Join-Path $kilde 'Skaermlup.Core/Settings/MachinePolicy.cs')
    $sti    = [regex]::Match($policy, 'RegistryPath\s*=\s*@"([^"]+)"').Groups[1].Value
    $vaerdi = [regex]::Match($policy, 'SampleImagesValue\s*=\s*"([^"]+)"').Groups[1].Value

    $admx = [xml](Get-Content -Raw $admxFil)
    foreach ($p in $admx.policyDefinitions.policies.policy) {
        if ($p.class -ne 'Machine') { "$($p.name): class er '$($p.class)', skal vaere Machine" }
        if ($p.key -ne $sti) { "$($p.name): key '$($p.key)', programmet laeser '$sti'" }
        if ($p.valueName -ne $vaerdi) { "$($p.name): valueName '$($p.valueName)', programmet laeser '$vaerdi'" }
    }

    # Hver tekst skabelonen henviser til, skal findes på alle sprog.
    $henvisninger = [regex]::Matches((Get-Content -Raw $admxFil), '\$\(string\.([^)]+)\)') |
                    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
    foreach ($adml in Get-ChildItem $mappe -Recurse -Filter *.adml) {
        $ider = ([xml](Get-Content -Raw $adml.FullName)).policyDefinitionResources.resources.stringTable.string.id
        $henvisninger | Where-Object { $_ -notin $ider } |
            ForEach-Object { "$($adml.Directory.Name)/$($adml.Name): mangler teksten '$_'" }
    }
}

Test-Paastand 'Programmet starter kun stifinderen, og kun fra advarselsvinduet' {
    $cs | Select-String -Pattern 'Process\.Start' |
        Where-Object { $_.Filename -ne 'SampleSavedWindow.xaml.cs' } |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Test-Paastand 'UI Automation laeser kun hvor et felt er, aldrig navn eller indhold (D11)' {
    $cs | Where-Object { (Get-Content $_.FullName -Raw) -match 'System\.Windows\.Automation' } |
        Select-String -Pattern '\.Current\.(?!BoundingRectangle\b)\w+|TextPattern|ValuePattern|LegacyIAccessible|SelectionPattern|\.GetText\(' |
        ForEach-Object { "$($_.Filename):$($_.LineNumber)  $($_.Line.Trim())" }
}

Write-Host ''
if ($fejl -gt 0) {
    Write-Host "$fejl paastand(e) holder ikke laengere. Ret docs/dataflow-og-compliance.md i samme commit." -ForegroundColor Red
    exit 1
}

Write-Host 'Alle paastande i dataflowsporingen holder.' -ForegroundColor Green
exit 0
