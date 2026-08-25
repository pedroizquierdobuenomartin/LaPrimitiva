@echo off
setlocal

pushd "%~dp0"
if errorlevel 1 (
    echo ERROR: No se ha podido acceder al directorio del proyecto.
    pause
    endlocal
    exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: No se ha encontrado el SDK de .NET en PATH.
    set "BUILD_EXIT=1"
    goto :finish
)

echo.
echo === Compilando LaPrimitiva en modo Debug ===
dotnet build "LaPrimitiva.sln" --configuration Debug
set "BUILD_EXIT=%ERRORLEVEL%"

:finish
echo.
if "%BUILD_EXIT%"=="0" (
    echo === Compilacion finalizada correctamente ===
) else (
    echo ERROR: La compilacion ha terminado con codigo %BUILD_EXIT%.
)
popd
pause
endlocal & exit /b %BUILD_EXIT%
