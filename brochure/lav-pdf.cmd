@echo off
rem Laver Skaermlup-brochure.pdf ud fra Skaermlup-brochure.html via Chrome.
rem Dobbeltklik, eller koer fra en terminal. Log i lav-pdf-log.txt.
setlocal
set "HERE=%~dp0"
set "LOG=%HERE%lav-pdf-log.txt"

set "CHROME=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not exist "%CHROME%" set "CHROME=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"
if not exist "%CHROME%" set "CHROME=%LocalAppData%\Google\Chrome\Application\chrome.exe"

echo Bruger: %CHROME% > "%LOG%"
if not exist "%CHROME%" (
  echo Chrome blev ikke fundet. Aabn i stedet Skaermlup-brochure.html i en browser >> "%LOG%"
  echo og vaelg Udskriv - Gem som PDF - papirstoerrelse A4 - marginer Standard. >> "%LOG%"
  goto :slut
)

rem Chrome vil have et rigtigt tempprofil-bibliotek naar den koerer headless.
set "PROFIL=%TEMP%\skaermlup-brochure-profil"

"%CHROME%" --headless=new --disable-gpu --no-first-run ^
  --user-data-dir="%PROFIL%" ^
  --no-pdf-header-footer ^
  --virtual-time-budget=4000 ^
  --print-to-pdf="%HERE%Skaermlup-brochure.pdf" ^
  "file:///%HERE:\=/%Skaermlup-brochure.html" >> "%LOG%" 2>&1

echo chrome exitcode=%errorlevel% >> "%LOG%"

if exist "%HERE%Skaermlup-brochure.pdf" (
  echo OK - PDF skrevet: %HERE%Skaermlup-brochure.pdf >> "%LOG%"
) else (
  echo PDF blev ikke skrevet. Se ovenstaaende. >> "%LOG%"
)

:slut
echo. >> "%LOG%"
echo FAERDIG >> "%LOG%"
