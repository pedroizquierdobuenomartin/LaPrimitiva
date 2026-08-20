# Plan de mejoras de La Primitiva

> Documento vivo para ejecutar de forma ordenada la auditoría realizada el 19 de agosto de 2026 sobre el commit `6f08f46`.

## Cómo utilizar este documento

- Trabajar **un hito cada vez**, respetando dependencias y prioridad.
- Cambiar `[ ]` por `[x]` solo cuando se cumplan todos sus criterios de aceptación.
- Implementar cada elemento en un cambio pequeño y verificable.
- Añadir debajo de cada elemento la fecha, commit o PR y una nota breve de la solución.
- No mezclar refactorizaciones estructurales con correcciones funcionales urgentes.
- Antes de cerrar un hito, ejecutar sus pruebas y comprobar manualmente los flujos afectados.

## Estado general

| Fase | Objetivo | Estado |
|---|---|---|
| 0 | Preparación y línea base | Completada |
| 1 | Integridad de datos y backups | Pendiente |
| 2 | Errores funcionales | Pendiente |
| 3 | Seguridad local robusta | Pendiente |
| 4 | Persistencia y arquitectura | Pendiente |
| 5 | Calidad, observabilidad y mantenimiento | Pendiente |
| 6 | Verificación final | Pendiente |

---

## Fase 0 — Preparación y línea base

### [x] M-000 — Crear una línea base verificable

**Objetivo:** poder demostrar que cada cambio mejora la aplicación sin introducir regresiones.

**Pasos:**

1. Documentar cómo arrancar la aplicación y la instancia SQL de desarrollo.
2. Identificar los flujos críticos: planes, registro, premios, Joker, dashboard, histórico, RSS, exportación y generación de combinaciones.
3. Definir una base de datos exclusiva para pruebas de integración.
4. Registrar el resultado inicial de las pruebas antes de corregir código.

**Criterios de aceptación:**

- Existe una conexión de pruebas que nunca apunta a `PrimitivaAuditV2` de desarrollo.
- Los flujos críticos están enumerados y tienen una comprobación reproducible.
- Se conoce qué pruebas fallaban antes de comenzar las correcciones.

**Ejecución:**

- **Fecha:** 20 de agosto de 2026.
- **Commit o referencia:** commit `M-000` (este commit), creado sobre `39d6d48`; evidencia en `mejoras/LINEA_BASE_M000.md`. La revisión funcional auditada sigue siendo `6f08f46`; `39d6d48` solo añadió este plan.
- **Pruebas realizadas antes de modificar:** `dotnet test LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --filter 'FullyQualifiedName!~Integration'` — 25 ejecutadas, 25 correctas y 0 fallidas. La conexión a `localhost\SQLEXPRESS` falló y las pruebas de integración no se ejecutaron para no tocar `PrimitivaAuditV2`.
- **Verificación del hito:** `scripts/Verify-M000Baseline.ps1` — correcta; valida la base `PrimitivaAuditV2_IntegrationTests`, la ausencia de usos directos inseguros en `UseSqlServer` y los nueve flujos críticos documentados. El JSON de pruebas también se validó con `ConvertFrom-Json`.
- **Resultado:** se documentaron el arranque y el estado inicial; se creó una conexión exclusiva con protección fail-fast basada en el sufijo `_IntegrationTests`; y se definió una matriz reproducible para planes, registro, premios, Joker, dashboard, histórico, RSS, exportación y generación.
- **Decisiones:** M-000 separa y bloquea la conexión, pero no crea, limpia ni elimina la base. El ciclo de vida determinista, las rutas portables del seeder y las ejecuciones consecutivas quedan reservados para M-103. No se compiló ni ejecutó la suite después de los cambios, conforme a la regla del repositorio; la comprobación posterior fue estática.

---

## Fase 1 — Integridad de datos y backups

> Es la fase más urgente. Una aplicación local sigue necesitando datos recuperables y pruebas que no destruyan información real.

### [x] M-101 — Corregir el servidor del backup

**Problema:** `scripts/BackupDatabases.ps1` utiliza `(localdb)\MSSQLLocalDB`, pero la aplicación trabaja con SQL Server Express.

**Pasos:**

1. Centralizar el nombre de instancia en un parámetro explícito.
2. Usar la misma instancia configurada para la aplicación.
3. Hacer que el script falle con código distinto de cero ante cualquier error de SQL o copia.
4. Evitar que elimine backups de bases de datos ajenas.

**Criterios de aceptación:**

- El backup procede de la instancia y base correctas.
- El script no informa éxito si `sqlcmd` falla.
- La retención solo elimina archivos gestionados por este script.

**Ejecución:**

- **Fecha:** 20 de agosto de 2026.
- **Commit o referencia:** commit `M-101` (este commit), creado sobre `ad39d46`; verificación reproducible en `scripts/Verify-M101Backup.ps1`.
- **Evidencia previa:** `LaPrimitiva.App/appsettings.json` configuraba `Server=localhost\SQLEXPRESS;Database=PrimitivaAuditV2`, mientras `scripts/BackupDatabases.ps1` invocaba `sqlcmd -S "(localdb)\MSSQLLocalDB"`, incluía `CuentasClarasDB`, capturaba los errores sin devolver un código de fallo y aplicaba la retención a todos los archivos `*.bak` del directorio.
- **Pruebas realizadas:** ejecución inicial en rojo de `scripts/Verify-M101Backup.ps1` contra el script anterior, que confirmó que no declaraba parámetros explícitos; ejecución final del mismo verificador — correcta. La prueba usa un doble de `sqlcmd` y valida la instancia `localhost\SQLEXPRESS`, la base `PrimitivaAuditV2`, el modificador `-b`, la creación y copia del archivo, los códigos distintos de cero ante fallos SQL y de copia, y que un `.bak` ajeno sobrevive a la retención.
- **Resultado:** el servidor, las bases, los destinos, la retención y el ejecutable de `sqlcmd` son parámetros explícitos con valores seguros por defecto; el backup predeterminado queda limitado a `PrimitivaAuditV2`; y cualquier error SQL, ausencia del archivo esperado, error de copia o error de limpieza finaliza con código `1` sin anunciar éxito.
- **Decisiones:** se añadió `-b` a `sqlcmd` para convertir errores SQL en códigos de salida; los archivos gestionados incorporan el marcador `_LaPrimitiva_` y solo ese patrón entra en retención; un destino remoto no montado conserva el comportamiento de backup solo local con advertencia, pero una copia iniciada que falle es fatal. No se ejecutó un backup real ni una restauración: la verificación de restauraciones pertenece exclusivamente a M-102. No se compiló la solución.

---

### [x] M-102 — Verificar restauraciones

**Problema:** crear un `.bak` no garantiza que pueda restaurarse.

**Pasos:**

1. Ejecutar `RESTORE VERIFYONLY` después de cada backup.
2. Generar checksum o hash del archivo.
3. Probar periódicamente una restauración en una base temporal.
4. Documentar ubicación, retención y procedimiento de recuperación.

**Criterios de aceptación:**

- Un backup corrupto provoca un fallo visible.
- Existe evidencia de al menos una restauración satisfactoria.
- La recuperación está documentada y no depende de conocimiento informal.

**Ejecución:**

- **Fecha:** 20 de agosto de 2026.
- **Commit o referencia:** commit `M-102` (este commit), creado sobre `b177788`; verificación reproducible en `scripts/Verify-M102Restore.ps1` y evidencia operativa en `mejoras/evidencias/M-102-restore-20260820.json`.
- **Evidencia previa:** `scripts/BackupDatabases.ps1` solo comprobaba que `sqlcmd` terminase con código cero y que existiese un `.bak`; no ejecutaba `RESTORE VERIFYONLY`, no generaba hash, no efectuaba una restauración temporal y no existía un procedimiento de recuperación fuera de este plan.
- **Pruebas realizadas:** análisis sintáctico PowerShell correcto para los cuatro scripts afectados; `scripts/Verify-M101Backup.ps1` correcto tras adaptar su doble a las dos llamadas SQL; `scripts/Verify-M102Restore.ps1` correcto, incluyendo un `RESTORE VERIFYONLY` simulado como corrupto que devuelve fallo visible y no distribuye ni firma el backup. Prueba operativa contra `localhost\LOCALSERVER`: backup real de `PrimitivaAuditV2` de 6.213.632 bytes, `RESTORE VERIFYONLY WITH CHECKSUM` correcto, SHA-256 `64c744d2eb425361399bb0be6fc522d5dd1e59c8ebadf9c88fea98e922b129d8`, restauración como `PrimitivaRestoreTest_M102_20260820`, `DBCC CHECKDB` correcto, eliminación de la base temporal confirmada con recuento cero y eliminación posterior del `.bak` temporal. No se compiló la solución.
- **Resultado:** cada backup se crea con checksums de página, se valida antes de copiarse y recibe un fichero `.sha256`; cualquier fallo de creación, verificación, hash o copia finaliza con código `1`. `scripts/Test-DatabaseRestore.ps1` realiza un simulacro seguro con `FILELISTONLY`, archivos físicos independientes, `DBCC CHECKDB`, evidencia JSON y limpieza de la base temporal. `mejoras/RECUPERACION_BACKUPS.md` documenta ubicaciones, retención, verificación, periodicidad y recuperación real.
- **Decisiones:** el simulacro exige el prefijo `PrimitivaRestoreTest_` y nunca puede sobrescribir `PrimitivaAuditV2`; se usa `MOVE` para impedir colisiones con sus archivos activos; la restauración temporal se elimina solo después de completarse correctamente para no ocultar un fallo a mitad de proceso; la validación funcional completa tras una recuperación real queda reservada a M-603. La prueba operativa usó `localhost\LOCALSERVER`, única instancia disponible en este equipo, sin cambiar el valor predeterminado `localhost\SQLEXPRESS` definido por la configuración de la aplicación y por M-101. M-103 no se ha iniciado.

### [ ] M-103 — Aislar completamente las pruebas de integración

**Problema:** las pruebas usan una conexión fija a LocalDB, rutas absolutas `f:\...` y un `ResetDatabaseAsync` vacío.

**Pasos:**

1. Crear una base efímera o exclusiva con nombre inequívoco de pruebas.
2. Sustituir rutas absolutas por recursos del propio proyecto de pruebas.
3. Implementar creación, migración, limpieza y eliminación deterministas.
4. Añadir una protección que rechace bases cuyo nombre no indique claramente que son de prueba.

**Criterios de aceptación:**

- Las pruebas no pueden modificar la base de desarrollo.
- Funcionan independientemente de la letra de unidad o carpeta del repositorio.
- Dos ejecuciones consecutivas producen el mismo resultado.

---

## Fase 2 — Errores funcionales

### [ ] M-201 — Corregir el guardado de sorteos desconectados

**Problema:** algunos sorteos se recuperan con `AsNoTracking()` y después se llama a `SaveChangesAsync()` sin adjuntar ni actualizar la entidad.

**Pasos:**

1. Confirmar el fallo con una prueba de integración.
2. Encapsular la actualización en un caso de uso o método de repositorio explícito.
3. Cargar y modificar una entidad seguida, o adjuntarla controlando qué propiedades cambian.

**Criterios de aceptación:**

- El cambio persiste después de crear un contexto nuevo.
- Solo se modifican las columnas previstas.
- Existe una prueba que falla con el comportamiento anterior.

### [ ] M-202 — Corregir la navegación a Registro

**Problema:** Planes navega a `/register`, pero la ruta real es `/registro`.

**Criterios de aceptación:**

- La navegación abre la página correcta.
- La ruta se obtiene de una constante o mecanismo que evite duplicación futura.

### [ ] M-203 — Unificar totales, premios y Joker

**Problema:** algunas rutas persisten `TotalCoste` y `TotalPremios` sin Joker, mientras otras propiedades calculadas sí lo incluyen.

**Pasos:**

1. Definir formalmente qué incluye cada total.
2. Centralizar el cálculo en dominio o aplicación.
3. Recalcular o migrar registros existentes si contienen datos incoherentes.
4. Añadir pruebas para Joker activado, desactivado, premiado y sin premio.

**Criterios de aceptación:**

- Dashboard, registro, resumen y ROI muestran los mismos totales.
- El total coincide con la suma visible de sus componentes.

### [ ] M-204 — Robustecer el parser RSS

**Problema:** la expresión regular admite separadores variables, pero el parseo divide únicamente por `" - "`; además, parte del trabajo diferido puede lanzar excepciones fuera del `try`.

**Criterios de aceptación:**

- Se parsean correctamente formatos con y sin espacios permitidos.
- Los errores se capturan donde se materializa la secuencia.
- Hay pruebas con entradas válidas, incompletas y malformadas.

### [ ] M-205 — Validar completamente los planes

**Reglas mínimas:**

- `EffectiveFrom <= EffectiveTo`.
- Costes y cantidades no negativos.
- `BetsPerDraw` dentro de un rango válido y aplicado realmente a los cálculos.
- Joker desactivado implica coste Joker cero.
- No hay periodos solapados incompatibles.

**Criterios de aceptación:**

- Las mismas reglas se aplican en UI, aplicación, dominio y SQL cuando corresponda.
- Una llamada que no pase por la UI tampoco puede guardar un plan inválido.

### [ ] M-206 — Preservar `CreatedAt` al actualizar planes

**Problema:** reconstruir la entidad y marcarla completa como modificada puede reemplazar su fecha de creación.

**Criterios de aceptación:**

- `CreatedAt` nunca cambia en una edición normal.
- `UpdatedAt` refleja la modificación.
- Una prueba comprueba explícitamente ambas propiedades.

---

## Fase 3 — Seguridad local robusta

### [ ] M-301 — Imponer técnicamente el límite local

**Severidad de auditoría:** media.

**Problema:** los perfiles actuales usan localhost, pero la aplicación no impide arrancar escuchando en una interfaz de red. No existe autenticación ni autorización.

**Decisión necesaria:**

- **Solo local:** rechazar al arrancar cualquier URL que no sea loopback y restringir hosts.
- **Acceso LAN futuro:** añadir autenticación, autorización por defecto y protección de todas las operaciones mutables y exportaciones.

**Criterios de aceptación para modo local:**

- La aplicación no arranca si intenta escuchar fuera de loopback.
- Una petición con host no permitido es rechazada.
- El comportamiento queda documentado.

### [ ] M-302 — Eliminar JavaScript mutable de CDN y añadir CSP

**Severidad de auditoría:** media.

**Problema:** Tailwind y Chart.js se cargan externamente sin versiones fijas, SRI ni CSP; además existe JavaScript inline.

**Pasos:**

1. Autoalojar las dependencias estáticas, opción recomendada para una aplicación local.
2. Eliminar JavaScript inline o autorizarlo mediante nonce/hash.
3. Añadir una CSP restrictiva.
4. Añadir `X-Content-Type-Options` y una política de referrer apropiada.

**Criterios de aceptación:**

- La aplicación funciona sin acceder a CDN externas.
- La CSP no necesita `unsafe-inline` ni orígenes comodín.

### [ ] M-303 — Validar rangos de sorteos históricos

**Severidad de auditoría:** baja.

**Problema:** números fuera de `1..49` pueden persistirse y provocar índices inválidos en el generador.

**Criterios de aceptación:**

- Números principales dentro de `1..49`, sin duplicados.
- Reintegro y Joker dentro de sus rangos válidos.
- El generador se defiende aunque reciba datos históricos corruptos.
- SQL Server contiene restricciones `CHECK` equivalentes cuando sea viable.

### [ ] M-304 — Limitar la descarga y el parseo RSS

**Severidad de auditoría:** baja.

**Criterios de aceptación:**

- Límites explícitos de bytes, elementos y tiempo.
- Uso de cancelación y lectura en streaming.
- Solo una actualización RSS simultánea.
- Un feed enorme o lento no bloquea indefinidamente el proceso.

### [ ] M-305 — Neutralizar fórmulas en exportaciones CSV

**Severidad de auditoría:** baja.

**Problema:** celdas que comienzan por `=`, `+`, `-` o `@` pueden convertirse en fórmulas al abrirse en Excel.

**Criterios de aceptación:**

- Las notas peligrosas se abren como texto literal.
- Se mantienen correctamente comillas, comas y saltos de línea.
- Existe una prueba con cada prefijo peligroso.

---

## Fase 4 — Persistencia y arquitectura

### [ ] M-401 — Sustituir DDL manual por migraciones EF Core

**Problema:** el arranque crea tablas con `IF OBJECT_ID` y no actualiza de forma segura esquemas anteriores.

**Criterios de aceptación:**

- El esquema completo puede crearse desde cero mediante migraciones.
- Una base anterior puede actualizarse sin perder datos.
- La aplicación normal no necesita permisos permanentes para crear tablas.

### [ ] M-402 — Usar contextos cortos con `IDbContextFactory`

**Problema:** un `DbContext` scoped puede vivir durante todo el circuito Blazor, acumular tracking y recibir operaciones concurrentes.

**Criterios de aceptación:**

- Cada operación crea y dispone su propio contexto.
- Ninguna entidad seguida se conserva como estado duradero de un componente.
- Las operaciones simultáneas no comparten un contexto EF.

### [ ] M-403 — Añadir control de concurrencia

**Criterios de aceptación:**

- Las entidades editables relevantes disponen de `rowversion` o token equivalente.
- Una actualización concurrente no sobrescribe datos silenciosamente.
- La UI informa del conflicto y permite recargar.

### [ ] M-404 — Reforzar límites entre capas

**Objetivo:** UI → casos de uso de Application → puertos/repositorios → Infrastructure.

**Pasos:**

1. Eliminar la referencia de Application hacia Infrastructure.
2. Evitar que los componentes accedan directamente al `DbContext`.
3. Centralizar reglas de costes, premios, ROI y validación.
4. Mantener persistencia y proveedores externos detrás de interfaces.

**Criterios de aceptación:**

- El dominio y Application no dependen de EF Core ni del proyecto Infrastructure.
- La misma regla de negocio no está duplicada en varias páginas.

### [ ] M-405 — Reemplazar eventos `async void` y liberar recursos

**Criterios de aceptación:**

- Los manejadores asíncronos devuelven `Task` o capturan y registran explícitamente sus errores.
- `MainLayout` libera `_feedbackTimer` y todas sus suscripciones.
- No quedan callbacks activos después de disponer un componente.

---

## Fase 5 — Calidad, observabilidad y mantenimiento

### [ ] M-501 — Completar la estrategia de pruebas

**Cobertura mínima:**

- Costes, premios, Joker y ROI.
- Rangos y duplicados de sorteos.
- Vigencia y solapamiento de planes.
- Persistencia de ediciones.
- Parser RSS y límites.
- Exportación CSV segura.
- Migraciones desde cero y desde una versión anterior.

### [ ] M-502 — Añadir observabilidad segura

**Criterios de aceptación:**

- Errores técnicos completos en logs estructurados.
- Mensajes de usuario comprensibles sin detalles internos sensibles.
- Logs para importación RSS, migraciones y backups.
- Health check básico de aplicación y base de datos.

### [ ] M-503 — Revisar dependencias

**Pasos:**

1. Alinear paquetes con el objetivo .NET 10.
2. Ejecutar análisis de paquetes vulnerables y obsoletos.
3. Automatizarlo en CI o en una comprobación periódica.

### [ ] M-504 — Eliminar código y artefactos innecesarios

**Elementos detectados:**

- Páginas de plantilla como Counter y Weather.
- `UnitTest1` vacío.
- Servicios registrados pero aparentemente no utilizados.
- `publish/` sin ignorar.
- `build_output.txt` si no forma parte de la documentación deliberada.

### [ ] M-505 — Aclarar el alcance del generador estadístico

**Problema:** existe un `pValue` marcador `-1.0` y la ponderación por números históricos puede dar una impresión matemática incorrecta.

**Criterios de aceptación:**

- No se presentan métricas simuladas como resultados reales.
- La UI explica que los sorteos son independientes y que el histórico no aumenta la probabilidad matemática de acierto.
- Se elimina código estadístico que no tenga una definición y prueba válidas.

---

## Fase 6 — Verificación final

### [ ] M-601 — Verificación funcional completa

- Ejecutar todas las pruebas unitarias y de integración.
- Recorrer manualmente cada flujo crítico.
- Comprobar los cálculos contra ejemplos conocidos.
- Crear, modificar y eliminar datos usando una base de pruebas.

### [ ] M-602 — Verificación de seguridad

- Repetir el análisis estático de seguridad.
- Ejecutar comprobación online de dependencias vulnerables.
- Verificar que la aplicación no escucha fuera de loopback.
- Probar límites del RSS y neutralización CSV.
- Confirmar CSP y ausencia de scripts externos mutables.

### [ ] M-603 — Simulacro de recuperación

- Crear un backup nuevo.
- Restaurarlo en una base temporal limpia.
- Arrancar la aplicación contra la copia restaurada.
- Verificar registros, planes, premios y totales.
- Documentar duración y pasos del proceso.

**Criterio de cierre del plan:** todas las fases anteriores están completadas, verificadas y asociadas a evidencia reproducible.

---

## Hallazgos descartados durante la auditoría

No se encontró evidencia confirmada de:

- SQL injection en consultas EF Core o en el seeder actual.
- SSRF en el cliente RSS, porque la URL está fijada en configuración controlada.
- XXE explotable con el uso actual de `XDocument` en .NET moderno.
- Inyección de comandos en el script de backup con sus valores estáticos actuales.
- CSRF convencional en los callbacks interactivos del circuito Blazor.

Estos descartes describen el código auditado y deben revisarse si cambian las fuentes de entrada o el modelo de despliegue.

## Limitaciones de la auditoría original

- Fue una auditoría estática del commit `6f08f46`.
- No incluyó pruebas dinámicas de la aplicación en ejecución.
- No se consultaron advisories online de dependencias.
- La severidad se calibró suponiendo uso local, considerando también exposición accidental por LAN o proxy.

## Registro de ejecución

| ID | Fecha | Commit/PR | Resultado | Notas |
|---|---|---|---|---|
| M-000 | 2026-08-20 | Commit `M-000` (este commit), sobre `39d6d48` | Completado | Línea base en `mejoras/LINEA_BASE_M000.md`; 25/25 pruebas no integradas correctas antes del cambio y verificación estática M-000 correcta después. |
| M-102 | 2026-08-20 | Commit `M-102` (este commit), sobre `b177788` | Completado | Backup real verificado y restaurado temporalmente; evidencia en `mejoras/evidencias/M-102-restore-20260820.json`; recuperación documentada. |
