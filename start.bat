@echo off
setlocal

rem DBTracker launcher.
rem Runs the Blazor app with the local SQL Server connection from appsettings.json
rem (Windows Integrated Security). The app applies EF Core migrations automatically
rem on startup, so no separate migration step is needed here.

cd /d "%~dp0src\DBTracker.Web"

echo Starting DBTracker...
echo Browser will open automatically once the server is ready.
echo Press Ctrl+C to stop.
echo.

dotnet run --launch-profile https

endlocal
