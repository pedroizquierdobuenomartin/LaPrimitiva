# Estrategia de pruebas — M-501

## Alcance y niveles

La estrategia separa tres señales para que un resultado no se atribuya al nivel equivocado:

1. **Verificación estática:** contratos que pueden demostrarse leyendo fuentes y configuración. No sustituye la ejecución de xUnit.
2. **Suite rápida:** dominio, Application, infraestructura aislada y comprobaciones de código fuente que no requieren SQL Server.
3. **Suite de integración:** migraciones y persistencia real contra SQL Server usando una base efímera y protegida.

Los verificadores PowerShell son una defensa adicional para hitos concretos. La regresión funcional vive en xUnit y debe fallar con código distinto de cero cuando se rompe un comportamiento cubierto.

## Matriz de cobertura mínima

| Área exigida | Nivel principal | Evidencia automatizada |
|---|---|---|
| Costes, premios, Joker y ROI | Rápida + integración | `DrawRecordTests`, `SummaryServiceTests`, `M404LayerBoundaryTests` y `FinancialTotalsRepairTests` |
| Rangos y duplicados de sorteos | Rápida | `WinningDrawTests`, `WinningDrawServiceTests` y `DrawServiceTests` |
| Vigencia y solapamiento de planes | Rápida + integración SQL | `PlanTests`, `PlanServiceTests` y `PlanIntegrationTests` |
| Persistencia de ediciones | Integración SQL | `DisconnectedDrawPersistenceTests`, `DrawRepositoryTrackingTests`, `PlanIntegrationTests` y `M403ConcurrencyIntegrationTests` |
| Parser RSS y límites | Rápida | `RssParserServiceTests`, `RssClientTests` y `DrawNotificationServiceTests` |
| Exportación CSV segura | Rápida | `CsvFieldFormatterTests` y `CsvExportBuilderTests` |
| Migraciones desde cero y desde una versión anterior | Integración SQL | `M401MigrationTests.Migrations_CreateTheCompleteSchema_FromScratch` y `Migrations_UpgradeFromPreviousVersion_WithoutLosingData`; se conserva además la adopción del esquema legado sin historial |

`scripts/Verify-M501TestStrategy.ps1` comprueba que la matriz, los comandos y los casos focalizados continúan presentes. La verificación se basa en nombres estables de pruebas; renombrar o reemplazar un caso exige actualizar conscientemente esta matriz y el verificador.

## Ejecución reproducible

Desde la raíz del repositorio, con dependencias restauradas y binarios compilados para la revisión actual:

```powershell
# Contrato documental y cobertura mínima de M-501
./scripts/Verify-M501TestStrategy.ps1

# Suite rápida, sin SQL Server
dotnet test ./LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --filter 'FullyQualifiedName!~Integration' --nologo

# Solo integración real
dotnet test ./LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --filter 'FullyQualifiedName~Integration' --nologo

# Cierre completo
dotnet test ./LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --nologo
```

`--no-build --no-restore` solo es válido cuando los binarios corresponden exactamente a las fuentes que se quieren validar. Una ejecución sobre binarios anteriores se registra como línea base previa, NUNCA como prueba posterior del cambio.

## Base de datos de integración

La configuración versionada apunta a `localhost\LOCALSERVER`, pero puede sustituirse sin editar el repositorio:

```powershell
$env:LAPRIMITIVA_INTEGRATION_TEST_CONNECTION = 'Server=localhost\LOCALSERVER;Database=PrimitivaAuditV2_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;Encrypt=False'
```

La conexión debe nombrar una base terminada en `_IntegrationTests`. El fixture deriva de ella un nombre único por proceso, aplica migraciones, limpia los datos entre casos mediante Respawn y elimina la base al finalizar. Las protecciones rechazan `PrimitivaAuditV2`, bases sin el sufijo y archivos adjuntos mediante `AttachDBFilename`.

Las pruebas de migración crean y eliminan su propia base segura. Cubren tres rutas diferentes: instalación vacía, adopción del antiguo esquema creado mediante `EnsureCreated` y actualización desde la migración de la versión anterior conservando filas y aplicando las migraciones pendientes.

La instancia versionada `LOCALSERVER` es exclusivamente local: tiene desactivados TCP/IP y Named Pipes, no fuerza cifrado y la suite usa autenticación integrada sobre el transporte local. `Microsoft.Data.SqlClient` activa cifrado de forma predeterminada; por eso la conexión declara `Encrypt=False` explícitamente y evita negociar TLS sobre un transporte que no lo admite. Esta excepción NO debe copiarse a una conexión remota.

`TrustServerCertificate=True` se conserva para overrides locales que habiliten un transporte cifrado con certificado autofirmado, pero no sustituye una cadena de confianza. M-501 no crea ningún certificado. Si otra instalación habilita TCP/IP o exige cifrado, debe usar `Encrypt=True` y entregar el certificado o la CA como artefactos instalables fuera de Git; una excepción de seguridad no cuenta como éxito.

## Criterio de cierre

M-501 se considera conforme cuando:

- las siete áreas de la matriz conservan pruebas focalizadas;
- la suite rápida termina sin fallos;
- la suite de integración termina sin fallos en un SQL Server compatible y nunca toca la base de desarrollo;
- las rutas de migración vacía, legado y versión anterior preservan esquema y datos;
- el verificador M-501 y `git diff --check` terminan correctamente;
- cualquier bloqueo ambiental se informa por separado y no se presenta como un fallo funcional ni como una prueba superada.
