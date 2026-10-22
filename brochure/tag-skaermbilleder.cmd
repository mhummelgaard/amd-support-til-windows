@echo off
rem Henter skaermbilleder af Skaermlup til brochuren.
rem
rem Programmet er skjult for al skaermkopiering - luppen kan altsaa ikke fotograferes
rem udefra. Derfor bruges programmets egen indbyggede funktion, der lader vinduerne
rem tegne sig selv: Skaermlup.exe --proevebillede=<mappe>
rem
rem Tip: taend luppen og laeg den hen over noget almindelig tekst FOER du koerer det her,
rem saa er det den tekst der staar i billedet af luppen.
setlocal
set "HERE=%~dp0"
set "BILLEDER=%HERE%billeder"
set "RAA=%HERE%billeder\raa"
set "LOG=%HERE%skaermbilleder-log.txt"

set "APP=%ProgramFiles%\Skaermlup\Skaermlup.exe"
if not exist "%APP%" set "APP=%ProgramFiles(x86)%\Skaermlup\Skaermlup.exe"

if not exist "%BILLEDER%" mkdir "%BILLEDER%"
if not exist "%RAA%" mkdir "%RAA%"

echo Bruger: %APP% > "%LOG%"
if not exist "%APP%" (
  echo Skaermlup er ikke installeret. Koer installer\out\Skaermlup-Setup-1.2.0.exe foerst. >> "%LOG%"
  goto :slut
)

"%APP%" --proevebillede=%RAA% >> "%LOG%" 2>&1
echo exitcode=%errorlevel% >> "%LOG%"

rem Vent lidt - programmet gemmer billederne og lukker selv hvis det ikke koerte i forvejen.
timeout /t 6 /nobreak >nul

echo. >> "%LOG%"
echo === GEMTE FILER === >> "%LOG%"
dir /b "%RAA%" >> "%LOG%" 2>&1

echo. >> "%LOG%"
echo === OMDOEBER TIL BROCHURENS NAVNE === >> "%LOG%"
if exist "%RAA%\MainWindow.png" (
  copy /y "%RAA%\MainWindow.png" "%BILLEDER%\kontrolpanel.png" >> "%LOG%" 2>&1
) else (
  echo MANGLER: MainWindow.png - aabn kontrolpanelet med Ctrl+Alt+S og koer igen >> "%LOG%"
)
if exist "%RAA%\LensWindow.png" (
  copy /y "%RAA%\LensWindow.png" "%BILLEDER%\lup-tekst.png" >> "%LOG%" 2>&1
) else (
  echo MANGLER: LensWindow.png - taend luppen og koer igen >> "%LOG%"
)

echo. >> "%LOG%"
echo BEMAERK: billeder\ring-startmenu.png kan ikke laves med et skaermklip. >> "%LOG%"
echo Luppen er sat til WDA_EXCLUDEFROMCAPTURE og kommer ikke med i nogen skaermkopi, >> "%LOG%"
echo og musemarkoeren kommer heller ikke med. Det billede skal tages med et kamera, >> "%LOG%"
echo eller sammensaettes i haanden. >> "%LOG%"

:slut
echo. >> "%LOG%"
echo FAERDIG >> "%LOG%"
