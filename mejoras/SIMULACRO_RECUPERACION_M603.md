# Simulacro de recuperación M-603

Este procedimiento completa la validación funcional que M-102 dejó expresamente pendiente. Crea un **backup nuevo**, lo restaura en una **base temporal** protegida, compara los datos y arranca un **binario existente** de la aplicación contra la copia. No compila, no migra y nunca reemplaza `PrimitivaAuditV2`.

## Ejecución

Requisitos: `LOCALSERVER` accesible con autenticación integrada, `sqlcmd`, `pwsh`, `dotnet`, el directorio de backup accesible por SQL Server y un binario de la aplicación previamente validado.

```powershell
pwsh -File .\scripts\Invoke-M603RecoveryDrill.ps1 `
  -ServerInstance 'localhost\LOCALSERVER' `
  -SourceDatabase 'PrimitivaAuditV2' `
  -BackupDirectory 'Z:\BBDD\Backups'
```

El script registra automáticamente una evidencia JSON bajo `mejoras/evidencias`. Puede indicarse otra ruta con `-EvidencePath`.

## Qué se verifica

1. Se obtiene una instantánea de los **registros**, **planes**, resultados históricos, registros jugados, registros con premios y **totales** financieros del origen.
2. `BackupDatabases.ps1` crea y valida un backup nuevo con `CHECKSUM`, `RESTORE VERIFYONLY` y SHA-256.
3. `Test-DatabaseRestore.ps1` restaura la copia como `PrimitivaRestoreTest_M603_*`, usa archivos físicos separados y ejecuta `DBCC CHECKDB`.
4. La instantánea restaurada debe coincidir campo a campo con el origen: recuentos, premios por tipo, gasto, premio total y neto.
5. Se arranca la aplicación sin build con una sobreescritura de proceso de `ConnectionStrings__DefaultConnection`; `/health/ready` debe indicar `Healthy` y las rutas de panel, planes, registro, histórico y datos deben devolver HTTP 200 y sus marcadores funcionales.
6. La aplicación se detiene y la base temporal se elimina incluso si una comprobación falla. El backup y su hash permanecen sujetos a la retención normal.

## Duración y evidencia

La evidencia conserva inicio, fin, duración total y tiempos separados de instantánea, backup, restauración/DBCC, comparación, arranque y rutas. También identifica el backup y el binario mediante tamaño y SHA-256. Un resultado solo es correcto si el JSON indica `successful`, las dos instantáneas son iguales, readiness es saludable y todas las rutas son HTTP 200.

## Seguridad y recuperación ante fallo

- Solo se aceptan nombres `PrimitivaRestoreTest_M603_*`; el origen no se pone en modo exclusivo ni se sobrescribe.
- El binario recibe la conexión restaurada solo en su entorno de proceso; no se modifica `appsettings.json`.
- La limpieza de la aplicación y de la base temporal vive en `finally`.
- Si falla una fase, conservar la salida, comprobar que no existe la base temporal y repetir con un backup nuevo después de corregir la causa. No usar este simulacro para restaurar producción.
