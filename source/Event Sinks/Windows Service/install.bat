@ECHO OFF

REM The following directory is for .NET 2.0
set DOTNETFX4=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319
set PATH=%PATH%;%DOTNETFX4%

echo Installing WindowsService...
echo ---------------------------------------------------
InstallUtil /i WebMonitoringSink.exe
echo ---------------------------------------------------
echo Starting Service
echo ---------------------------------------------------
net start WebEventSinkService
echo Done.