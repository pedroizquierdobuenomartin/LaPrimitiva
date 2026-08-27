# Taxonomía transversal de errores (M-506)

## Regla de uso

La aplicación usa excepciones semánticas para condiciones cuyo tratamiento operativo es distinto. Domain declara el contrato estable; Infrastructure traduce EF Core, SQL Server y HTTP; Application decide si un caso de uso propaga una excepción o la convierte en `Result` tipado; App presenta únicamente `SafeMessage`/`ApplicationError.Message` y registra el detalle técnico en la frontera.

No se crea una excepción por cada validación. `BusinessRuleException` y `DataIntegrityException` incluyen un código específico (`RuleCode`/`IntegrityCode`) y contexto estructurado. El tipo expresa la estrategia de recuperación; el código identifica el caso concreto y permitirá a M-507 resolver recursos localizados sin depender de `Exception.Message`.

## Catálogo aprobado

| Categoría | Traduce o crea | Severidad | Reintento | Mensaje seguro | Recuperación |
| --- | --- | --- | --- | --- | --- |
| `BusinessRule` | Domain/Application | Warning | No | Regla concreta, sin datos técnicos | Corregir entrada |
| `NotFound` | Application/Infrastructure | Warning | No | El elemento ya no está disponible | Volver |
| `Concurrency` | Infrastructure desde `DbUpdateConcurrencyException` | Warning | No, sin recargar | Otro usuario modificó el registro | Recargar |
| `Integrity` | Application e Infrastructure desde SQL 2601/2627/547 | Warning | No | Conflicto con datos registrados/relacionados | Corregir entrada |
| `PersistenceUnavailable` | Infrastructure desde errores SQL de conexión/operación | Error | Sí | Datos temporalmente inaccesibles | Reintentar |
| `ExternalUnavailable` | Infrastructure desde HTTP o timeout | Warning | Sí | Proveedor temporalmente no disponible | Reintentar |
| `ExternalInvalidData` | Infrastructure/Application desde límites y parseo RSS | Warning | Sí | Respuesta externa no procesable | Reintentar |
| `Unexpected` | Frontera HTTP o Blazor | Error | No automático | Error inesperado registrado | Volver |

Los números SQL Server no reconocidos y los `DbUpdateException` sin una violación conocida se traducen a `PersistenceOperationException`, que conserva la categoría `Unexpected` y la excepción interna. No se marcan como reintentables ni se disfrazan de integridad: podría tratarse de un defecto de programación o de esquema.

## Responsabilidad por frontera

- **Domain:** valida invariantes mediante `BusinessRuleException`; no conoce EF Core, SQL Server, HTTP, logging ni componentes.
- **Infrastructure:** `PersistenceExceptionTranslator` conserva cancelación y excepciones semánticas; traduce concurrencia, unicidad/integridad y disponibilidad. `RssClient` distingue cancelación, timeout, formato/límite e indisponibilidad HTTP.
- **Application:** propaga excepciones semánticas en comandos generales. El flujo de históricos conserva `Result`, pero ahora su error es `ApplicationError` tipado; nunca es una cadena arbitraria.
- **App:** los manejadores esperados presentan `ApplicationError.Message`. Las capturas generales existen únicamente en fronteras de evento, servicio transversal, health check o circuito; llaman a `IApplicationErrorReporter`, que registra una vez la excepción completa con `ErrorCode`, `ErrorCategory`, `Operation` y `ErrorReference`.
- **UI global:** `/Error` y `AppErrorBoundary` muestran una referencia comunicable, no trazas, SQL, cadenas de conexión ni mensajes del proveedor.

## Cancelación y timeout

`OperationCanceledException` se conserva cuando el token solicitado por el llamador está cancelado. No se registra como fallo inesperado. Solo se crea `ExternalServiceTimeoutException` cuando la cancelación representa el límite temporal del proveedor o el límite propio de la sincronización RSS.

## Excepciones estándar conservadas

- `OperationCanceledException`: contrato estándar de cancelación cooperativa; envolverla rompería la semántica de tareas y tokens.
- `ArgumentOutOfRangeException`: se mantiene para argumentos de programación fuera del contrato, como solicitar una plantilla para un día que no tiene sorteo. No es una regla recuperada desde persistencia ni un mensaje de proveedor.
- `ArgumentException` al resolver un charset HTTP desconocido: se captura de forma específica y aplica UTF-8 como fallback deliberado; no representa un fallo del caso de uso.
- `InvalidOperationException` en la política de arranque local: señala configuración insegura del host y debe impedir el inicio. No se convierte en error de negocio recuperable.
- Las excepciones inesperadas de programación se conservan con su pila original hasta la frontera. No se ocultan dentro de `BusinessRuleException` ni de un `Result` fallido.

## Datos seguros en logs

El scope transversal registra solo código, categoría, operación y referencia. El contexto semántico se limita a identificadores, códigos de regla, proveedor y duración; no debe contener apuestas, notas de usuario, SQL, secretos ni cadenas de conexión. La excepción completa queda en el sink seguro configurado por M-502 y nunca se concatena en el texto mostrado a la persona usuaria.
