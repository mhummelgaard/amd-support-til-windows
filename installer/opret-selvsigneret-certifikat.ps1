# Laver et hjemmelavet kodesigneringscertifikat, så Skærmlup kan bygges med uiAccess og dermed
# ligge oven på Start-menuen og resten af Windows' egen skal.
#
# Samme metode som ESDH PDS bruger til sit prøvecertifikat (installer\make-test-certificate.ps1).
#
#   Kør som administrator:
#   pwsh installer\opret-selvsigneret-certifikat.ps1
#
#   Prøv det først uden at røre maskinens tillid:
#   pwsh installer\opret-selvsigneret-certifikat.ps1 -Proeve
#
# Hvad det gør, i almindelige ord:
#   1. Laver en "rod" — et stempel der siger "dette certifikat er ægte". En maskine, der stoler
#      på roden, stoler på det, den har stemplet.
#   2. Stempler ét certifikat med den: Skærmlups signeringscertifikat. Det kan kun signere
#      programmer — ikke websteder, og ikke andre certifikater.
#   3. Fortæller denne pc, at roden er til at stole på.
#   4. Smider rodens hemmelige nøgle væk. En betroet rod, der stadig kan stemple, kunne stemple
#      et certifikat for et hvilket som helst websted, og maskinen ville tro på det. Denne
#      stempler præcis ét certifikat og kan aldrig stemple et til. Fornyelse betyder at køre
#      scriptet igen og installere den nye rod.
#   5. Gemmer rodens offentlige del som en fil, til andre maskiner du selv har hånd om.
#
# Signeringscertifikatets egen nøgle bliver på maskinen — den skal bruges ved hvert byg. Den kan
# ikke eksporteres. Kommer den alligevel i de forkerte hænder, kan den signere programmer, som
# denne pc stoler på. Den kan ikke udstede noget som helst.
#
# HVAD DET IKKE LØSER: andre maskiner end dem, roden er installeret på, kender den ikke.
# SmartScreen advarer stadig hos alle andre. Skal Skærmlup ud til brugere, er det et rigtigt
# kodesigneringscertifikat fra en udsteder der skal til. Se G1 i docs\dataflow-og-compliance.md.
#
# Bygget med Windows' egen New-SelfSignedCertificate, så der er intet at hente.
[CmdletBinding()]
param(
    [string] $Navn = $env:COMPUTERNAME,
    [string] $UdMappe = (Join-Path $PSScriptRoot 'out'),

    # Gør det hele i brugerens eget lager, efterprøver kæden, signerer en prøvefil og rydder op
    # igen. Rører hverken maskinens betroede rødder eller betroede udgivere.
    [switch] $Proeve
)

$ErrorActionPreference = 'Stop'

$emne = "CN=Skaermlup signering ($Navn)"
$rodEmne = "CN=Skaermlup signeringsrod ($Navn)"

if (-not $Proeve) {
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin) { throw 'Kør scriptet som administrator: roden skal ind i maskinens liste over betroede udstedere.' }

    $findes = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq $emne
    if ($findes) {
        throw "Der findes allerede et certifikat for $emne ($($findes.Thumbprint -join ', ')). " +
              'Fjern det først, så byggeriet ikke har to at vælge imellem.'
    }
}

New-Item -ItemType Directory -Force -Path $UdMappe | Out-Null
$rod = $null
$cert = $null

try {
    # 1. Roden. pathlength=0: den kan stemple et signeringscertifikat, aldrig en ny rod.
    $rod = New-SelfSignedCertificate -Type Custom `
        -Subject $rodEmne `
        -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
        -KeyUsage CertSign, CRLSign, DigitalSignature `
        -TextExtension @('2.5.29.19={critical}{text}ca=1&pathlength=0') `
        -NotAfter (Get-Date).AddYears(5) `
        -CertStoreLocation Cert:\CurrentUser\My

    # 2. Signeringscertifikatet: kun kodesignering, og nøglen kan ikke eksporteres. Det udløber
    #    før roden; signaturer med tidsstempel holder stadig efter udløb.
    $cert = New-SelfSignedCertificate -Type CodeSigningCert `
        -Subject $emne `
        -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears(4) `
        -Signer $rod `
        -CertStoreLocation Cert:\CurrentUser\My

    # 5. Rodens offentlige del som fil.
    $cer = Join-Path $UdMappe "Skaermlup-signeringsrod-$Navn.cer"
    Export-Certificate -Cert $rod -FilePath $cer -Type CERT | Out-Null
    $rodAftryk = $rod.Thumbprint

    # 4. Rodens hemmelige nøgle, væk. Herefter kan den ikke stemple noget.
    Remove-Item -Path "Cert:\CurrentUser\My\$rodAftryk" -DeleteKey
    $rod = $null

    if (-not $Proeve) {
        # 3. Roden betroet på maskinen, og signeringscertifikatet som betroet udgiver.
        Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        $udgiver = Join-Path $UdMappe "Skaermlup-signering-$Navn.cer"
        Export-Certificate -Cert $cert -FilePath $udgiver -Type CERT | Out-Null
        Import-Certificate -FilePath $udgiver -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
    }

    # Efterprøv: bygger kæden, kan certifikatet signere, og er rodens nøgle reelt væk?
    $kaede = [System.Security.Cryptography.X509Certificates.X509Chain]::new()
    $kaede.ChainPolicy.RevocationMode = 'NoCheck'
    if ($Proeve) {
        $kaede.ChainPolicy.ExtraStore.Add([System.Security.Cryptography.X509Certificates.X509Certificate2]::new($cer)) | Out-Null
        $kaede.ChainPolicy.VerificationFlags = 'AllowUnknownCertificateAuthority'
    }
    $betroet = $kaede.Build($cert)

    $proevefil = Join-Path $UdMappe 'signeringsproeve.ps1'
    Set-Content -Path $proevefil -Value "# Skaermlup signeringsproeve`n'ok'" -Encoding utf8
    $signatur = Set-AuthenticodeSignature -FilePath $proevefil -Certificate $cert -HashAlgorithm SHA256
    $kanSignere = $signatur.SignerCertificate.Thumbprint -eq $cert.Thumbprint
    Remove-Item $proevefil -Force

    $rodNoegleVaek = -not (Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $rodAftryk -and $_.HasPrivateKey })

    Write-Host ''
    Write-Host "Signeringscertifikat : $emne" -ForegroundColor Green
    Write-Host "Aftryk               : $($cert.Thumbprint)"
    Write-Host "Gyldigt til          : $($cert.NotAfter.ToString('d'))"
    Write-Host "Kæden bygger         : $betroet"
    Write-Host "Kan signere          : $kanSignere"
    Write-Host "Roden kan stemple    : $(if ($rodNoegleVaek) { 'nej — dens hemmelige nøgle er slettet' } else { 'JA — noget er galt' })"
    Write-Host "Rod til andre pc'er  : $cer"
    Write-Host "Rodens aftryk        : $rodAftryk  (sammenlign det på den anden pc)"

    if (-not ($betroet -and $kanSignere -and $rodNoegleVaek)) { throw 'Efterprøvningen fejlede. Se ovenfor.' }

    if ($Proeve) {
        Write-Host ''
        Write-Host 'Prøve: alt virker. Ryddet op igen — maskinens tillid er ikke rørt.' -ForegroundColor Cyan
    }
    else {
        Write-Host ''
        Write-Host 'Byg nu installeren med:' -ForegroundColor Green
        Write-Host "  pwsh installer\build-installer.ps1 -CertificateThumbprint $($cert.Thumbprint)" -ForegroundColor Green
        Write-Host ''
        Write-Host 'Fortryder du, fjernes det hele igen med:' -ForegroundColor Yellow
        Write-Host "  Get-ChildItem Cert:\LocalMachine\Root, Cert:\LocalMachine\TrustedPublisher |" -ForegroundColor Yellow
        Write-Host "    Where-Object Thumbprint -in '$rodAftryk', '$($cert.Thumbprint)' | Remove-Item" -ForegroundColor Yellow
        Write-Host "  Remove-Item Cert:\CurrentUser\My\$($cert.Thumbprint) -DeleteKey" -ForegroundColor Yellow
    }
}
finally {
    # Går noget galt halvvejs, må der aldrig blive en rod tilbage med nøglen i behold.
    if ($rod) { Remove-Item -Path "Cert:\CurrentUser\My\$($rod.Thumbprint)" -DeleteKey -ErrorAction SilentlyContinue }

    if ($Proeve) {
        if ($cert) { Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey -ErrorAction SilentlyContinue }
        # -DeleteKey findes kun når certifikat-udbyderen er bundet, så den skal have en sti —
        # for enden af en tom pipeline kendes parameteren slet ikke.
        Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq $rodEmne | ForEach-Object {
            Remove-Item -Path "Cert:\CurrentUser\My\$($_.Thumbprint)" -DeleteKey -ErrorAction SilentlyContinue
        }
        Remove-Item (Join-Path $UdMappe "Skaermlup-signeringsrod-$Navn.cer") -ErrorAction SilentlyContinue
    }
}
