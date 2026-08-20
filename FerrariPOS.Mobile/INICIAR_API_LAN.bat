@echo off
setlocal
set ASPNETCORE_URLS=http://0.0.0.0:5080
cd /d "%~dp0API"
echo ================================================
echo FerrariPOS Mobile API - LAN
echo Puerto: 5080
echo ================================================
dotnet run
pause
