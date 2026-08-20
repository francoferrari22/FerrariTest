@echo off
setlocal
cd /d "%~dp0"
echo Compilando API...
dotnet publish API -c Release -r win-x64 --self-contained false -o Publicado\API
if errorlevel 1 goto error
 echo Compilando sincronizador Windows...
dotnet publish SyncAgent -c Release -r win-x64 --self-contained false -o Publicado\SyncAgent
if errorlevel 1 goto error
echo Listo. Carpetas en Publicado\
pause
exit /b 0
:error
echo Error de compilacion. Instale .NET 8 SDK.
pause
exit /b 1
