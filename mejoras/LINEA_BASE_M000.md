# M-000 — Línea base verificable

Fecha de captura: **20 de agosto de 2026**

Revisión funcional auditada: **`6f08f46`**

Revisión base del hito: **`39d6d48`** (solo añade el plan de mejoras sobre la revisión auditada)

Ámbito: preparación y evidencia; no incluye correcciones de los hitos M-101 en adelante.

## 1. Arranque reproducible

### Requisitos

- SDK .NET `10.0.400` o una versión compatible con `net10.0`.
- SQL Server Express con una instancia denominada `SQLEXPRESS`.
- Autenticación integrada de Windows habilitada para el usuario que ejecuta la aplicación.

La aplicación de desarrollo usa `localhost\SQLEXPRESS` y la base `PrimitivaAuditV2`. Esa base **no debe utilizarse para pruebas de integración**.

### Preparar SQL Server

Desde PowerShell con permisos para administrar servicios:

```powershell
Get-Service 'MSSQL$SQLEXPRESS'
Start-Service 'MSSQL$SQLEXPRESS' # solo si aparece detenido
sqlcmd -S 'localhost\SQLEXPRESS' -E -C -Q 'SELECT @@SERVERNAME, DB_NAME();'
```

Si el servicio `MSSQL$SQLEXPRESS` no existe, hay que instalar o configurar esa instancia antes de arrancar. No se debe sustituir silenciosamente por otra instancia porque la aplicación y las pruebas dejarían de reproducir la misma topología.

### Arrancar la aplicación

Desde la raíz del repositorio:

```powershell
dotnet run --project .\LaPrimitiva.App\LaPrimitiva.App.csproj --launch-profile http
```

Abrir `http://localhost:5007`. El arranque ejecuta el seeding de históricos, por lo que una conexión SQL válida es un requisito previo.

## 2. Base exclusiva de integración

La configuración versionada está en `LaPrimitiva.Tests/appsettings.IntegrationTests.json` y apunta a:

```text
localhost\SQLEXPRESS / PrimitivaAuditV2_IntegrationTests
```

Todas las pruebas de integración obtienen la conexión mediante `IntegrationTestDatabase`. La protección rechaza cualquier nombre que no termine en `_IntegrationTests`, incluida la base de desarrollo `PrimitivaAuditV2`.

Para usar otra instancia se admite exclusivamente la variable:

```powershell
$env:LAPRIMITIVA_INTEGRATION_TEST_CONNECTION = 'Server=MI_SERVIDOR\MI_INSTANCIA;Database=PrimitivaAuditV2_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True'
```

El sufijo sigue siendo obligatorio. La creación, limpieza y eliminación deterministas de esta base pertenecen a **M-103**; M-000 solo establece el destino separado y el bloqueo contra bases no identificadas como pruebas.

Comprobación rápida, sin conectarse a SQL ni compilar:

```powershell
.\scripts\Verify-M000Baseline.ps1
```

## 3. Flujos críticos y comprobación reproducible

Para una comprobación manual se parte de una base de integración preparada y de un año sin datos previos. Cada evidencia debe anotar fecha, resultado y datos usados.

| ID | Flujo | Comprobación reproducible | Cobertura automática inicial |
|---|---|---|---|
| FLOW-PLANES | Planes | En `/planes`, crear un plan para un año vacío, editar su nombre y comprobar que vuelve a mostrarse tras recargar. | Parcial: reglas y servicio; integración no ejecutada. |
| FLOW-REGISTRO | Registro | Generar sorteos desde el plan, abrir `/registro`, marcar uno como jugado y comprobar el valor tras recargar. | Parcial: servicio; integración no ejecutada. |
| FLOW-PREMIOS | Premios | En `/registro`, introducir premios de fija y automática, recargar y contrastar premio total y neto. | Parcial: cálculos de dominio. |
| FLOW-JOKER | Joker | Crear un plan con Joker, registrar costes y premios Joker y contrastar componentes y totales tras recargar. | Parcial: activación y coste calculado. |
| FLOW-DASHBOARD | Dashboard | Abrir `/`, seleccionar el año preparado y contrastar gasto, premio, neto y ROI con los registros visibles. | Sin comprobación de UI. |
| FLOW-HISTORICO | Histórico | Abrir `/historico`, crear o editar un sorteo válido y comprobar que persiste tras recargar. | Parcial: servicio de sorteos ganadores. |
| FLOW-RSS | RSS | Materializar un XML conocido con el parser, guardar un resultado no duplicado y confirmar su aparición en Histórico. | Parcial: parser y mapeo unitarios. |
| FLOW-EXPORTACION | Exportación | Abrir `/datos`, exportar todo y verificar cabecera, número de filas, comillas y totales del CSV. | Sin prueba automática inicial. |
| FLOW-GENERACION | Generación | Abrir `/combinacion-automatica`, generar una combinación y comprobar seis números únicos `1..49` más reintegro válido. | Sin prueba automática inicial. |

## 4. Resultado inicial antes de correcciones

Comandos ejecutados el **20 de agosto de 2026** sobre el código funcional de `6f08f46`, antes de modificar M-000. El commit base actual `39d6d48` solo incorporó el plan documental:

```powershell
dotnet --version
dotnet test .\LaPrimitiva.Tests\LaPrimitiva.Tests.csproj --filter 'FullyQualifiedName!~Integration' --logger 'console;verbosity=normal'
Get-Service -Name 'MSSQL*','SQLBrowser'
sqlcmd -S 'localhost\SQLEXPRESS' -E -l 5 -Q 'SELECT @@SERVERNAME;'
```

Resultado:

- SDK: `10.0.400`.
- Pruebas no integradas: **25 ejecutadas, 25 correctas, 0 fallidas**.
- Restauración/compilación previa a los cambios: correcta, con un aviso `CS9113` y avisos `NU1903` ya existentes para `System.Security.Cryptography.Xml 9.0.0`.
- SQL detectado: servicio `MSSQL$LOCALSERVER` activo; no se detectó `MSSQL$SQLEXPRESS`.
- Conexión a `localhost\SQLEXPRESS`: falló; por tanto no se arrancó la aplicación ni se ejecutaron pruebas de integración.
- Pruebas de integración iniciales: **no ejecutadas deliberadamente**, porque antes de M-000 apuntaban a `PrimitivaAuditV2` y podían modificar datos de desarrollo. Además, `ResetDatabaseAsync` está vacío y el seeder conserva rutas absolutas; ambos problemas quedan expresamente para M-103.

Esta es la línea base: las pruebas unitarias existentes pasan, pero los flujos completos todavía carecen de cobertura reproducible y la instancia SQL configurada no está disponible en el equipo observado.
