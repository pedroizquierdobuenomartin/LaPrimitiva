@echo off
setlocal

pushd "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: No se ha encontrado el SDK de .NET en PATH.
    goto :fail
)

echo.
echo === Restaurando herramientas locales ===
dotnet tool restore
if errorlevel 1 goto :fail

echo.
echo === Publicando LaPrimitiva en modo Release ===
dotnet publish "LaPrimitiva.App\LaPrimitiva.App.csproj" --configuration Release --output "%~dp0publish"
if errorlevel 1 goto :fail

echo.
echo === Generando paquete portable de migraciones ===
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\New-DatabaseMigrationBundle.ps1" -Configuration Release -OutputDirectory "%~dp0publish" -NoBuild
if errorlevel 1 goto :fail

echo.
echo Publicacion completada en:
echo %~dp0publish
echo.
echo IMPORTANTE: En el equipo de destino ejecute ActualizarBaseDatos.bat
echo despues de copiar la publicacion y antes de iniciar IIS.
popd
pause
endlocal
exit /b 0

:fail
echo.
echo ERROR: La publicacion no ha terminado correctamente.
popd
pause
endlocal
exit /b 1
