; Inno Setup-script til Skærmlup — forstørrelsesglas til skærmen.
; Byg med:  installer\build-skaermlup.ps1   (publicerer self-contained win-x64 og oversætter dette).
; Indstillingerne ligger pr. bruger under %LocalAppData%\Skaermlup, så en afinstallation
; ikke rører brugerens egne valg.

#define AppName "Skaermlup"
#define AppDisplayName "Skærmlup"
#define AppVersion "1.4.0"
#define Publisher "Michael Hummelgaard"
#define ExeName "Skaermlup.exe"
#define PublishDir "..\src\Skaermlup.Desktop\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
AppId={{B4E71C90-2D6A-4F13-9A55-SKLUP0000001}
AppName={#AppDisplayName}
AppVersion={#AppVersion}
AppVerName={#AppDisplayName} {#AppVersion}
AppPublisher={#Publisher}
; Gratis for alle — men udvikleren skal fremgå. Står også i installerens filegenskaber.
AppCopyright=© 2026 {#Publisher} · Gratis for alle (MIT-licens)
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
; MIT-licensen kræver, at copyright-linjen med udviklerens navn følger med hver kopi.
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppDisplayName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\Afinstallér {#AppDisplayName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppDisplayName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "Start {#AppDisplayName}"; Flags: nowait postinstall skipifsilent

[Code]
{ ---------------------------------------------------------------------------------------------
  Skærmlup ud og ind uden at musepilen forsvinder.

  Den der opgraderer eller afinstallerer, har som regel luppen tændt — det er sådan de kan læse
  installeren. Skal filerne skiftes ud, mens programmet kører, lukker Windows det. Gør Windows
  det med magt, når programmet ikke at give musepilen tilbage, og ved en afinstallation starter
  det aldrig igen og kan ikke rydde op selv. Så sidder brugeren uden musepil.

  Derfor: bed først programmet om at lukke sig selv pænt, og vent på det. Og bagefter, uanset
  hvad: ligger vagtfilen der stadig, sættes musepilen tilbage her.

  Navnene skal passe med src\Skaermlup.Desktop\Services\InstanceSignals.cs.
  --------------------------------------------------------------------------------------------- }

const
  InstanceKey = 'Skaermlup.EnKoerselAdGangen.v1';
  EVENT_MODIFY_STATE = $0002;
  SPI_SETCURSORS = $0057;
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

function OpenEvent(dwDesiredAccess: Cardinal; bInheritHandle: Boolean; lpName: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(hEvent: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function SystemParametersInfo(uiAction: Cardinal; uiParam: Cardinal; pvParam: Cardinal; fWinIni: Cardinal): Boolean;
  external 'SystemParametersInfoW@user32.dll stdcall';

function SkaermlupKoerer: Boolean;
begin
  Result := CheckForMutexes(InstanceKey);
end;

{ Sender en besked til det kørende program og venter op til 10 sekunder på, at det lukker.
  Ældre udgaver kender ikke beskeden; så lukkes de af Windows som hidtil. }
function BedSkaermlupLukke(const Besked: String): Boolean;
var
  Signal: THandle;
  I: Integer;
begin
  Result := not SkaermlupKoerer;
  if Result then Exit;

  Signal := OpenEvent(EVENT_MODIFY_STATE, False, InstanceKey + '.' + Besked);
  if Signal = 0 then
  begin
    Log('Skaermlup koerer, men tager ikke imod beskeden ' + Besked + ' (aeldre udgave?)');
    Exit;
  end;

  SetEvent(Signal);
  CloseHandle(Signal);

  for I := 1 to 50 do
  begin
    Sleep(200);
    if not SkaermlupKoerer then
    begin
      Log('Skaermlup lukkede paent efter ' + IntToStr(I * 200) + ' ms');
      Result := True;
      Exit;
    end;
  end;

  Log('Skaermlup lukkede ikke inden for 10 sekunder');
end;

{ Sidste sikkerhedsnet. Programmet lægger en vagtfil, mens musepilen er skjult, og fjerner den
  igen, når pilen er tilbage. Ligger den der stadig, er programmet gået ned med pilen skjult.
  Kun for den bruger, der kører installeren — er det en anden administrator, ville dennes egne
  markører blive lagt ind hos brugeren, og så gøres der hellere ingenting. }
procedure GenopretMusepil;
var
  Vagt: String;
begin
  Vagt := ExpandConstant('{localappdata}\Skaermlup\musepil-skjult');
  if not FileExists(Vagt) then Exit;

  SystemParametersInfo(SPI_SETCURSORS, 0, 0, 0);
  DeleteFile(Vagt);
  Log('Musepilen var efterladt skjult og er sat tilbage');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  BedSkaermlupLukke('Afslut');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  { Før sidste side, så brugeren har en musepil at trykke "Udfør" med. }
  if CurStep = ssPostInstall then GenopretMusepil;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    usUninstall:
      { Programmet fjerner selv sin autostart. Det kører som brugeren og kan nå den. }
      BedSkaermlupLukke('Afinstaller');
    usPostUninstall:
      begin
        { Kørte programmet ikke, gøres det her — for den bruger, der afinstallerer. }
        RegDeleteValue(HKEY_CURRENT_USER, RunKey, 'Skaermlup');
        GenopretMusepil;
      end;
  end;
end;
