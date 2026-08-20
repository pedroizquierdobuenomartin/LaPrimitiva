@echo off
setlocal

pushd "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: No se ha encontrado el SDK de .NET en PATH.
    goto :fail
)

echo.
echo === Compilando LaPrimitiva en modo Debug ===
dotnet build "LaPrimitiva.sln" --configuration Debug
if errorlevel 1 goto :fail

echo.
echo === Ejecutando LaPrimitiva en http://localhost:5007 ===
dotnet run --project ".\LaPrimitiva.App\LaPrimitiva.App.csproj" --no-build --launch-profile http
if errorlevel 1 goto :fail

popd
endlocal
exit /b 0

:fail
echo.
echo ERROR: El proceso no ha terminado correctamente.
popd
pause
endlocal
exit /b 1
