@echo off
cd /d "%~dp0SyncAgent"
echo FerrariPOS Mobile Sync Agent
echo Primero configure appsettings.json
dotnet run
pause
