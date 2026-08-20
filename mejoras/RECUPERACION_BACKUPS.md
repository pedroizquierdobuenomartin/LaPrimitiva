# Recuperación de backups de LaPrimitiva

Este procedimiento corresponde al hito M-102. Su objetivo es que la recuperación sea reproducible y no dependa de conocimiento informal.

## Ubicación y retención

- **Origen SQL Server:** `localhost\SQLEXPRESS`, base `PrimitivaAuditV2` por defecto. Ambos valores pueden pasarse explícitamente a los scripts.
- **Copia local:** `Z:\BBDD\Backups` por defecto.
- **Segunda copia:** `G:\Mi unidad\BBDD\Backups` por defecto. Si el directorio no está montado, el script avisa y conserva únicamente la copia local.
- **Retención local:** 7 días por defecto, configurable con `-DaysToKeepLocal`. Solo se eliminan pares gestionados `PrimitivaAuditV2_LaPrimitiva_*.bak[.sha256]`.
- **Integridad:** cada `.bak` se crea con `CHECKSUM`, pasa `RESTORE VERIFYONLY ... WITH CHECKSUM` y tiene un fichero `.sha256` asociado. Un fallo impide copiarlo al segundo destino y el proceso termina con código 1.

## Crear un backup verificado

```powershell
pwsh -File .\scripts\BackupDatabases.ps1
```

Para una instancia o ubicación distinta:

```powershell
pwsh -File .\scripts\BackupDatabases.ps1 `
  -ServerInstance 'localhost\SQLEXPRESS' `
  -LocalBackupDir 'Z:\BBDD\Backups' `
  -DriveBackupDir 'G:\Mi unidad\BBDD\Backups'
```

Debe finalizar con código 0 y mostrar que `RESTORE VERIFYONLY` y SHA-256 fueron correctos. Para comprobar el hash posteriormente:

```powershell
$bak = 'Z:\BBDD\Backups\PrimitivaAuditV2_LaPrimitiva_AAAAMMDD_HHMMSS.bak'
(Get-FileHash -LiteralPath $bak -Algorithm SHA256).Hash.ToLowerInvariant()
Get-Content -LiteralPath "$bak.sha256"
```

Los valores deben coincidir antes de una recuperación.

## Simulacro periódico de restauración

Ejecutar al menos una vez al mes y después de cambios en SQL Server, permisos, destinos o scripts:

```powershell
pwsh -File .\scripts\Test-DatabaseRestore.ps1 `
  -ServerInstance 'localhost\SQLEXPRESS' `
  -BackupFile 'Z:\BBDD\Backups\PrimitivaAuditV2_LaPrimitiva_AAAAMMDD_HHMMSS.bak' `
  -TemporaryDatabaseName 'PrimitivaRestoreTest_20260820' `
  -EvidencePath '.\mejoras\evidencias\M-102-restore-20260820.json'
```

El script:

1. vuelve a ejecutar `RESTORE VERIFYONLY`;
2. obtiene los nombres lógicos mediante `RESTORE FILELISTONLY`;
3. restaura con `MOVE` a archivos temporales independientes;
4. ejecuta `DBCC CHECKDB`;
5. escribe evidencia JSON si se indicó `-EvidencePath`;
6. elimina la base temporal tras el éxito.

Por seguridad, el nombre temporal debe empezar por `PrimitivaRestoreTest_`. El script nunca reemplaza ni elimina `PrimitivaAuditV2`.

## Recuperación real

1. Detener la aplicación y conservar el `.bak` y su `.sha256` sin modificarlos.
2. Confirmar el SHA-256 y ejecutar primero el simulacro anterior.
3. Crear un backup adicional de la base actual si sigue accesible.
4. Restaurar mediante SQL Server Management Studio o un procedimiento DBA aprobado. No reutilizar el script de simulacro para sobrescribir producción: deliberadamente solo acepta nombres `PrimitivaRestoreTest_*`.
5. Arrancar la aplicación contra la base recuperada y validar registros, planes, premios y totales. Esa validación funcional completa corresponde a M-603.
6. Registrar operador, fecha, backup usado, hash, duración y resultado.

Si cualquier comprobación falla, no distribuir ni restaurar ese archivo: conservar los logs, generar un backup nuevo y escalar el incidente.
