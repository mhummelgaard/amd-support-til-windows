; Inno Setup-script til Skærmlup — forstørrelsesglas til skærmen.
; Byg med:  installer\build-skaermlup.ps1   (publicerer self-contained win-x64 og oversætter dette).
; Indstillingerne ligger pr. bruger under %LocalAppData%\Skaermlup, så en afinstallation
; ikke rører brugerens egne valg.

#define AppName "Skaermlup"
#define AppDisplayName "Skærmlup"
#define AppVersion "1.2.0"
#define Publisher "Michael Hummelgaard"
#define ExeName "Skaermlup.exe"
#define PublishDir "..\src\Skaermlup.Desktop\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
AppId={{B4E71C90-2D6A-4F13-9A55-SKLUP0000001}
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppVerName={#AppDisplayName} {#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\Skaermlup
DefaultGroupName={#AppDisplayName}
UninstallDisplayIcon={app}\{#ExeName}
UninstallDisplayName={#AppDisplayName}
SetupIconFile=..\src\Skaermlup.Desktop\Assets\skaermlup.ico
OutputDir=out
OutputBaseFilename=Skaermlup-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
DisableProgramGroupPage=yes

[Languages]
Name: "danish"; MessagesFile: "compiler:Languages\Danish.isl"

[Tasks]
Name: "desktopicon"; Description: "Opret en genvej på skrivebordet"; GroupDescription: "Genveje:"

; "Start sammen med Windows" står med vilje ikke her. Installeren kører som administrator,
; så en HKCU-nøgle ville lande hos administratoren og ikke hos den der skal bruge luppen.
; Afkrydsningsfeltet i programmets eget kontrolpanel skriver den for den rigtige bruger.

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppDisplayName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\Afinstallér {#AppDisplayName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "Start {#AppDisplayName}"; Flags: nowait postinstall skipifsilent
