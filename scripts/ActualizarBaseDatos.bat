@echo off
setlocal EnableExtensions

pushd "%~dp0"

if not exist "LaPrimitiva.DatabaseMigration.exe" (
    echo ERROR: No se encuentra LaPrimitiva.DatabaseMigration.exe.
    goto :fail
)

if exist "ESQUEMA_BD.version" (
    echo.
    for /f "usebackq delims=" %%V in ("ESQUEMA_BD.version") do echo === Esquema incluido: %%V ===
)

echo.
echo El bundle comprobara __EFMigrationsHistory y aplicara solo las migraciones pendientes.
echo Ejecute este archivo despues de copiar CADA publicacion y antes de iniciar IIS.
echo.

if defined LAPRIMITIVA_MIGRATION_CONNECTION (
    "LaPrimitiva.DatabaseMigration.exe" --connection "%LAPRIMITIVA_MIGRATION_CONNECTION%"
) else (
    if not exist "appsettings.json" (
        echo ERROR: No existe appsettings.json y no se ha definido LAPRIMITIVA_MIGRATION_CONNECTION.
        goto :fail
    )
    "LaPrimitiva.DatabaseMigration.exe"
)

if errorlevel 1 goto :fail

echo.
echo Base de datos preparada correctamente. Ya puede iniciar IIS.
popd
pause
endlocal
exit /b 0

:fail
echo.
echo ERROR: La base de datos no se ha podido preparar. No inicie la aplicacion.
popd
pause
endlocal
exit /b 1
