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

### [x] M-103 — Aislar completamente las pruebas de integración

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

**Cierre:**

- **Fecha:** 2026-08-20.
- **Commit o referencia:** commit `M-103` (este commit), creado sobre `d48abb7`.
- **Evidencia previa:** el repositorio estaba limpio en `d48abb7`. `IntegrationTestDatabase` solo validaba una base fija, `IntegrationTestBase.ResetDatabaseAsync()` no hacía nada y `WinningDrawSeederTests` leía dos CSV desde `f:\Repositorios\LaPrimitiva\.agent\assests`. La ejecución previa `dotnet test LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --filter FullyQualifiedName~Integration --no-restore` confirmó que la suite no era reproducible en este equipo: 6 pruebas descubiertas, 1 correcta y 5 fallidas; la conexión fija a `localhost\SQLEXPRESS` no encontró la instancia y varios fallos secundarios intentaron escribir en el Event Log de Windows.
- **Pruebas realizadas:** antes del cambio, ejecución dinámica anterior con resultado 1/6. Después del cambio, análisis sintáctico PowerShell correcto y `scripts/Verify-M103IntegrationIsolation.ps1` ejecutado dos veces consecutivas con resultado correcto en ambas; verifica nombre efímero protegido, migraciones, limpieza Respawn, eliminación, serialización de la colección, recurso portable y ausencia de rutas absolutas o conexiones que omitan el fixture. También se comprobó por reflexión la API usada de Respawn 7.0.0 y `git diff --check` sin errores. No se compiló ni ejecutó la suite modificada, conforme a la regla del repositorio de no construir después de cambios.
- **Resultado:** cada sesión de pruebas genera un nombre único que conserva el sufijo obligatorio `_IntegrationTests`; el fixture aplica migraciones, reinicia datos antes de cada prueba mediante Respawn conservando `__EFMigrationsHistory` y elimina la base al terminar. Todas las pruebas de integración comparten una colección no paralela y la aplicación omite su seeding normal únicamente en el entorno `IntegrationTests`. El seeder usa ahora `LaPrimitiva.Tests/TestData/winning-draws.csv`, copiado al directorio de salida por el proyecto de pruebas.
- **Decisiones:** se mantiene SQL Server para comprobar el proveedor y las migraciones reales, en vez de sustituirlo por EF InMemory o SQLite; se crea una base por ejecución, no una base fija compartida, para aislar procesos simultáneos; toda operación destructiva vuelve a validar el sufijo de seguridad; y Respawn limpia las tablas sin borrar el historial de migraciones. No se ha iniciado M-201.

---

## Fase 2 — Errores funcionales

### [x] M-201 — Corregir el guardado de sorteos desconectados

**Problema:** algunos sorteos se recuperan con `AsNoTracking()` y después se llama a `SaveChangesAsync()` sin adjuntar ni actualizar la entidad.

**Pasos:**

1. Confirmar el fallo con una prueba de integración.
2. Encapsular la actualización en un caso de uso o método de repositorio explícito.
3. Cargar y modificar una entidad seguida, o adjuntarla controlando qué propiedades cambian.

**Criterios de aceptación:**

- El cambio persiste después de crear un contexto nuevo.
- Solo se modifican las columnas previstas.
- Existe una prueba que falla con el comportamiento anterior.

**Cierre:**

- **Fecha:** 2026-08-20.
- **Commit o referencia:** commit `M-201` (este commit), creado tras M-103.
- **Evidencia previa:** `DrawRepository.GetListAsync()` devolvía `DrawRecord` mediante `AsNoTracking()`, mientras `Register.SaveDraw()` modificaba esa instancia y solo invocaba `DrawRepository.SaveChangesAsync()`. Al no existir ninguna entrada seguida ni estado `Modified`, EF Core no generaba una actualización. Además, el `UpdateAsync()` disponible usaba `DbSet.Update()`, marcando todas las propiedades como modificadas y sin limitar las columnas editables.
- **Pruebas realizadas:** análisis sintáctico PowerShell correcto para `scripts/Verify-M201DisconnectedDrawSave.ps1`; ejecución del verificador M-201 correcta; y `git diff --check` sin errores. Se añadió `DisconnectedDrawPersistenceTests.UpdateAsync_PersistsEditableValuesWithoutChangingStructuralColumns`, que parte de la consulta sin seguimiento, actualiza mediante el repositorio y recarga con un contexto nuevo, comprobando tanto los valores editables como la preservación de `PlanId`, tipo, fecha, semana y `CreatedAt`. El usuario compiló la solución correctamente, con 0 errores y 8 advertencias `NU1903` preexistentes sobre `System.Security.Cryptography.Xml` 9.0.0, y verificó manualmente contra `PrimitivaAuditV2` en `localhost\LOCALSERVER` que la edición guarda, refleja los cambios y persiste tras recargar. La suite de integración modificada no se ejecutó.
- **Resultado:** `Register.SaveDraw()` delega ahora en `IDrawRepository.UpdateAsync()`. El repositorio vuelve a cargar la fila como entidad seguida y copia explícitamente únicamente estado de juego, costes, premios, totales editables, notas y `UpdatedAt`; después persiste en una sola llamada. Se eliminó el `SaveChangesAsync()` genérico del contrato para impedir que la UI vuelva a confiar en el seguimiento accidental.
- **Decisiones:** se eligió cargar la entidad seguida en vez de adjuntar el objeto completo, porque permite una lista blanca clara de columnas y conserva identidad, plan, fecha, tipo, semana, acumulado y fecha de creación. `UpdateRangeAsync()` reutiliza la misma lista blanca para que el guardado modal no mantenga una segunda semántica más permisiva. No se ha iniciado M-202.

### [x] M-202 — Corregir la navegación a Registro

**Problema:** Planes navega a `/register`, pero la ruta real es `/registro`.

**Criterios de aceptación:**

- La navegación abre la página correcta.
- La ruta se obtiene de una constante o mecanismo que evite duplicación futura.

- **Fecha:** 2026-08-20.
- **Commit o referencia:** commit `M-202` (este commit), sobre `4a7cfdf` (`fix: persist disconnected draw updates`).
- **Evidencia previa:** `Plans.razor` ejecutaba `Nav.NavigateTo("/register")`, mientras `Register.razor` declaraba únicamente `@page "/registro"`; la búsqueda de rutas no encontró ningún alias `/register` ni una constante compartida, por lo que la acción de Planes apuntaba a una URL sin página asociada.
- **Pruebas realizadas:** se añadió `scripts/Verify-M202RegistrationNavigation.ps1`; primero se ejecutó en rojo porque no existía `LaPrimitiva.App/AppRoutes.cs`. Tras implementar el hito, el análisis sintáctico PowerShell fue correcto, el verificador M-202 terminó correctamente, la búsqueda estática confirmó que aplicación y destino consumen `AppRoutes.Registration`, y `git diff --check` no detectó errores. El usuario compiló y ejecutó la aplicación y verificó manualmente que desde Planes se accede correctamente a Registro mediante `/registro`.
- **Resultado:** Planes navega ahora mediante `AppRoutes.Registration`, cuyo valor es `/registro`, y la propia página Registro obtiene su `RouteAttribute` de esa misma constante. La ruta incorrecta `/register` ya no aparece en el código de la aplicación.
- **Decisiones:** se sustituyó `@page` por un `RouteAttribute` basado en constante para que origen y destino compartan una única fuente de verdad; cambiar solo el literal de `NavigateTo` habría arreglado el fallo inmediato, pero mantendría la duplicación que el criterio de aceptación prohíbe. No se ha iniciado M-203.

### [x] M-203 — Unificar totales, premios y Joker

**Problema:** algunas rutas persisten `TotalCoste` y `TotalPremios` sin Joker, mientras otras propiedades calculadas sí lo incluyen.

**Pasos:**

1. Definir formalmente qué incluye cada total.
2. Centralizar el cálculo en dominio o aplicación.
3. Recalcular o migrar registros existentes si contienen datos incoherentes.
4. Añadir pruebas para Joker activado, desactivado, premiado y sin premio.
5. Comprobar que el ROI se calcula correctamente.

**Criterios de aceptación:**

- Dashboard, registro, resumen y ROI muestran los mismos totales.
- El total coincide con la suma visible de sus componentes.

**Cierre (2026-08-20):**

- **Referencia:** commit `M-203` (este commit), sobre `07a58cc`.
- **Pruebas realizadas:** línea base previa `dotnet test LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --no-build --filter "FullyQualifiedName~DrawRecordTests"` (3/3 correctas); `scripts/Verify-M203FinancialTotals.ps1` correcto tras la implementación; `git diff --check` correcto; build, ejecución y comprobación visual satisfactorios comunicados por el usuario. Se añadieron pruebas unitarias para Joker activado/desactivado, premiado/sin premio y ROI, una prueba de persistencia y otra de reparación de datos; el agente no volvió a compilar tras los cambios por la política del repositorio.
- **Resultado:** `DrawRecord.RecalculateFinancials` define una única regla: coste total = fija + automática + Joker fija + Joker automática; premios totales siguen la misma composición y neto = premios − coste. Registro muestra y edita los componentes Joker, Dashboard/resúmenes/planes consumen los totales unificados y el ROI deriva de ellos. El repositorio impone el invariante al crear y actualizar, y el arranque repara de forma idempotente registros anteriores incoherentes.
- **Decisiones:** se conservaron los importes por componente como fotografía histórica y se centralizó solo su agregación en dominio; se evitó recalcular desde el plan salvo al activar/desactivar `Played`, porque cambios posteriores del plan no deben reescribir costes históricos. No se modificó `BetsPerDraw`, cuya validación y aplicación pertenece expresamente a M-205, ni se avanzó a M-204.

### [x] M-204 — Robustecer el parser RSS

**Problema:** la expresión regular admite separadores variables, pero el parseo divide únicamente por `" - "`; además, parte del trabajo diferido puede lanzar excepciones fuera del `try`.

**Criterios de aceptación:**

- Se parsean correctamente formatos con y sin espacios permitidos.
- Los errores se capturan donde se materializa la secuencia.
- Hay pruebas con entradas válidas, incompletas y malformadas.

**Cierre (2026-08-20):**

- **Referencia:** commit `M-204` (este commit), sobre `5b0c243` (`fix: unify financial totals with Joker`).
- **Evidencia previa:** con la entrada válida `04-05-13-29-30-36`, la expresión regular existente encontraba la combinación (`Success = true`), pero `Split(" - ")` producía un único elemento y `int.Parse` lanzaba una excepción. Además, `ParseRss()` devolvía el `Select(ParseItem)` sin materializar, por lo que una `FormatException` generada al enumerar escapaba del `try`; ambas conductas se reprodujeron antes de editar.
- **Pruebas realizadas:** línea base previa `dotnet test LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --no-build --filter "FullyQualifiedName~RssParserServiceTests"` (2/2 correctas sobre los binarios existentes); análisis sintáctico correcto y ejecución satisfactoria de `scripts/Verify-M204RssParser.ps1`; `git diff --check` sin errores. Se añadieron casos xUnit para separadores sin espacios y con espacios irregulares, entrada incompleta, sorteo malformado con materialización segura y XML malformado. Esos casos nuevos no se ejecutaron porque la política del repositorio prohíbe compilar después de los cambios. El usuario verificó en ejecución que los sorteos obtenidos mediante RSS aparecen y se guardan correctamente.
- **Resultado:** el parser separa ahora por el carácter `-`, recorta cada segmento y descarta segmentos vacíos, manteniendo alineado el parseo con los espacios que admite la expresión regular. La proyección de elementos se materializa mediante `ToArray()` dentro del `try`, de modo que cualquier error de parseo diferido queda capturado y produce una colección vacía en vez de escapar al consumidor.
- **Decisiones:** se mantuvo el contrato tolerante actual —elementos incompletos o malformados se omiten y un feed inválido devuelve una colección vacía— y no se amplió el alcance con validación de rangos, cambios del cliente RSS ni trabajo de M-205. Se eligió `StringSplitOptions.TrimEntries | RemoveEmptyEntries` frente a otra expresión regular para que el separador aceptado tenga una única semántica simple y explícita.

### [x] M-205 — Validar completamente los planes

**Reglas mínimas:**

- `EffectiveFrom <= EffectiveTo`.
- Costes y cantidades no negativos.
- `BetsPerDraw` dentro de un rango válido y aplicado realmente a los cálculos.
- Joker desactivado implica coste Joker cero.
- No hay periodos solapados incompatibles.

**Criterios de aceptación:**

- Las mismas reglas se aplican en UI, aplicación, dominio y SQL cuando corresponda.
- Una llamada que no pase por la UI tampoco puede guardar un plan inválido.

**Cierre (2026-08-20):**

- **Referencia:** commit `M-205` (este commit), sobre `a39f7a2` (`fix: harden RSS draw handling`).
- **Evidencia previa:** `PlanService` solo comprobaba el nombre y los solapamientos, mientras `PlanRepository` guardaba directamente; `Plan` no validaba fechas, costes ni cantidades y nacía con Joker desactivado pero coste Joker `0,50`. La UI no permitía editar `BetsPerDraw`, EF y el DDL manual carecían de restricciones `CHECK`, y `DrawRecord.RecalculateFinancials()` cobraba siempre una apuesta fija y una automática con independencia de `BetsPerDraw`. La línea base previa `dotnet test LaPrimitiva.Tests/LaPrimitiva.Tests.csproj --no-build --filter "FullyQualifiedName~PlanTests|FullyQualifiedName~PlanServiceTests"` pasó 7/7 pruebas sobre los binarios existentes, confirmando que la cobertura anterior no ejercitaba estas reglas.
- **Pruebas realizadas:** línea base previa de planes 7/7 correcta; `scripts/Verify-M205PlanValidation.ps1` correcto; `git diff --check` sin errores. Se añadieron 14 casos xUnit para límites y valores inválidos de dominio, aplicación real de `BetsPerDraw`, rechazo al omitir la UI y el servicio, restricciones SQL y trigger de solapamientos. Las pruebas nuevas no se ejecutaron porque la política del repositorio prohíbe compilar después de los cambios.
- **Resultado:** `Plan.Validate()` concentra las reglas estructurales y se invoca desde UI, Application, repositorio y cálculo de costes. La UI expone `BetsPerDraw` con límites y restringe fechas/costes; el repositorio vuelve a validar y rechaza solapamientos aunque se omita el servicio. EF, una migración y el inicializador DDL incorporan restricciones `CHECK`; un trigger SQL impide carreras o escrituras directas con periodos solapados. Los costes base y Joker ya multiplican realmente el número de apuestas configurado.
- **Decisiones:** `BetsPerDraw` admite `1..100`; una apuesta se registra como fija y las restantes se agregan en el componente automático, preservando el modelo histórico de dos componentes. El Joker se cobra por cada apuesta. Los costes Joker históricos de planes con Joker desactivado se normalizan a cero antes de activar las restricciones; los solapamientos históricos no se corrigen de forma arbitraria, sino que bloquean la activación para exigir una decisión explícita. Se mantuvieron las fotografías de coste ya persistidas en sorteos anteriores y no se avanzó a M-206.
- **Corrección posterior (2026-08-20):** la verificación manual al editar un plan reveló el error SQL Server 334: EF generaba `OUTPUT` sobre `Plans` sin conocer el trigger de solapamientos. Se declaró `TR_Plans_PreventOverlap` en el modelo y se desactivó explícitamente `UseSqlOutputClause` para esa tabla, conservando el trigger y usando el guardado compatible. El test de actualización existente se renombró para registrar expresamente esta regresión; `scripts/Verify-M205PlanValidation.ps1` y `git diff --check` volvieron a resultar correctos. Referencia: commit `e315855` (`fix: support plan trigger writes`), sobre `32d7876`. El usuario reconstruyó la aplicación y confirmó posteriormente que la edición del plan se guarda correctamente en ejecución.

### [x] M-206 — Preservar `CreatedAt` al actualizar planes

**Problema:** reconstruir la entidad y marcarla completa como modificada puede reemplazar su fecha de creación.

**Criterios de aceptación:**

- `CreatedAt` nunca cambia en una edición normal.
- `UpdatedAt` refleja la modificación.
- Una prueba comprueba explícitamente ambas propiedades.

**Cierre (2026-08-24):**

- **Referencia:** commit de cierre M-206 de esta publicación, sobre `373561a`; release `v1.2.0`.
- **Evidencia previa:** `Plans.razor` reconstruía para la edición una entidad `Plan` sin copiar `CreatedAt`, por lo que el inicializador de la entidad le asignaba un `DateTime.UtcNow` nuevo. `PlanRepository.UpdateAsync` desconectaba cualquier instancia seguida y marcaba esa entidad completa como `EntityState.Modified`, incluyendo `CreatedAt`; en consecuencia, el siguiente `SaveChangesAsync` podía reemplazar la fecha de creación persistida. El repositorio ya actualizaba `UpdatedAt`, pero no protegía `CreatedAt`.
- **Pruebas realizadas:** línea base estática previa `scripts/Verify-M205PlanValidation.ps1` correcta; tras el cambio, `scripts/Verify-M206PlanTimestamps.ps1` correcto, regresión estática M-205 correcta y `git diff --check` sin errores. Se añadió `UpdatePlan_ShouldPreserveCreatedAt_AndRefreshUpdatedAt`, que envía una entidad desconectada con un `CreatedAt` manipulado y comprueba explícitamente que la fecha original permanece y que `UpdatedAt` avanza. La prueba xUnit nueva no se ejecutó porque la política del repositorio prohíbe compilar después de los cambios.
- **Resultado:** `PlanRepository.UpdateAsync` carga el plan persistido y copia únicamente las propiedades editables. `CreatedAt` permanece en la entidad seguida y no se incluye en la actualización; `UpdatedAt` se establece en UTC en el momento de guardar.
- **Decisiones:** se eligió actualizar una entidad seguida mediante lista blanca en vez de mantener el patrón de entidad desconectada marcada completamente como modificada. Así la protección no depende de que cada consumidor recuerde reenviar `CreatedAt`, se preservan también las navegaciones y se evita aceptar desde el exterior tanto `CreatedAt` como `UpdatedAt`. No se avanzó al siguiente hito.

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

---

## Fase 7 — Hallazgos y mejoras emergentes

Esta es una fase viva para registrar problemas y oportunidades confirmados durante el uso real y la ejecución del plan que no estuvieran contemplados en la auditoría original.

**Reglas de incorporación:**

- Cada hallazgo debe incluir evidencia reproducible y un hito independiente.
- Registrar el problema no implica implementarlo ni alterar el hito que esté en curso.
- Cada hito debe definir criterios de aceptación y pruebas antes de cerrarse.
- Los hitos aplicables de esta fase deben completarse antes del cierre definitivo del plan; si modifican comportamiento ya verificado, se repetirá la parte afectada de la Fase 6.

### [ ] M-701 — Detectar todos los sorteos RSS pendientes aunque existan huecos históricos

**Problema:** el popup calcula los sorteos pendientes conservando únicamente los posteriores a la fecha más reciente del histórico. Si se guarda primero el sorteo RSS más nuevo, esa fecha pasa a ser el límite y desaparecen del popup sorteos anteriores todavía no guardados. El flujo obliga incorrectamente a guardar del más antiguo al más reciente.

**Evidencia (2026-08-20):** el usuario reprodujo el fallo durante la verificación real de M-204. `DrawNotificationService.CheckForNewDrawsAsync()` consulta `GetLatestDrawDateAsync()` y aplica `d.Date.Date > latestHistoricalDate.Value.Date`; después de cada guardado, `MainLayout.SaveToHistory()` vuelve a ejecutar esa comprobación. Por tanto, una fecha máxima no permite distinguir huecos anteriores.

**Comportamiento esperado:**

- El popup muestra cada sorteo presente en el RSS que todavía no exista en el histórico, independientemente de que sea anterior o posterior a la última fecha guardada.
- Los sorteos pueden guardarse en cualquier orden.
- Tras guardar un sorteo desaparece únicamente ese sorteo; los demás pendientes permanecen disponibles.
- Un sorteo ya registrado no vuelve a ofrecerse ni puede duplicarse.

**Criterios de aceptación:**

- La detección compara el conjunto del RSS con los sorteos realmente almacenados mediante una identidad estable, en vez de usar solo la fecha máxima como marcador.
- Existe una prueba con un histórico que contiene el sorteo más reciente pero conserva huecos anteriores.
- Existe una prueba que guarda varios sorteos en orden no cronológico y verifica los pendientes después de cada guardado.
- La restricción de unicidad por fecha continúa protegiendo frente a duplicados.

**Dependencias:** M-204.

### [x] M-702 — Evaluar y rediseñar la generación de apuestas automáticas

**Problema:** tras aproximadamente seis meses de uso real, el usuario indica que las combinaciones generadas automáticamente no han obtenido aciertos apreciables frente a la combinación fija. El generador actual presenta su resultado como una sugerencia basada en análisis bayesiano, pero no existe una evaluación retrospectiva que demuestre que supere a una selección aleatoria uniforme ni a la apuesta fija.

**Evidencia (2026-08-24):** `AutomatedCombinationService.GenerateCombinationAsync()` pondera únicamente la frecuencia individual de los números con decaimiento temporal de 365 días y suavizado de Dirichlet, selecciona seis números mediante muestreo aleatorio ponderado y usa una semilla semanal. El botón `Regenerar` vuelve a invocar el servicio con esa misma semilla, por lo que devuelve la misma combinación mientras no cambie el histórico. El reintegro es uniforme y el valor p de la prueba chi-cuadrado es un marcador `-1`. No existen pruebas específicas del servicio ni backtesting walk-forward. El resultado de los seis meses es evidencia de uso aportada por el usuario; la base actual conserva premios y costes, pero no los números de las apuestas fijas o automáticas, así que esa comparación no puede reconstruirse a nivel de combinación.

**Restricción estadística:** si los sorteos son independientes y uniformes, el histórico no permite predecir de forma fiable la combinación siguiente. La mejora deberá evitar prometer capacidad predictiva no demostrada y medir cualquier modelo fuera de muestra contra una línea base aleatoria.

**Alternativas a valorar:**

1. **Motor validado por backtesting (opción recomendada):** crear un evaluador walk-forward que compare el modelo actual, selección uniforme y variantes de hiperparámetros sin utilizar sorteos futuros. Adoptar un modelo solo si mejora de forma estable métricas predefinidas fuera de muestra; en caso contrario, usar la línea base honesta.
2. **Optimizador de cobertura y diversificación:** dejar de intentar adivinar el siguiente sorteo y generar una o varias apuestas con mínima repetición entre sí, cobertura controlada y exclusión opcional de patrones populares. No aumenta la probabilidad de una combinación individual, pero aprovecha mejor un presupuesto de varias apuestas y puede reducir el riesgo de compartir premio.
3. **Modelo de relaciones y ensemble temporal:** ampliar las frecuencias individuales con pares, tríos, intervalos entre apariciones, suma, paridad y ventanas temporales, combinando modelos regularizados. Solo sería admisible si supera las líneas base en backtesting walk-forward y mantiene el rendimiento en periodos separados; tiene el mayor coste y riesgo de sobreajuste.

**Decisión (2026-08-24):** comenzar por la alternativa 1. La primera entrega incorporará un backtest walk-forward reproducible contra selección uniforme y corregirá `Regenerar` para solicitar una variación nueva sin cambiar el modelo estadístico evaluado.

**Criterios para decidir:**

- Medir por separado la capacidad predictiva, la cobertura conseguida y el coste por sorteo.
- Comparar siempre contra selección uniforme, modelo actual y apuesta fija con el mismo número de apuestas y fechas.
- Reservar un periodo final que no participe en el ajuste y publicar resultados reproducibles, incluidos los resultados negativos.
- Favorecer la alternativa más sencilla que demuestre mejora fuera de muestra; si ninguna predice mejor, presentar la generación como diversificación y no como predicción.

**Criterios de aceptación del hito:**

- Existe un backtest walk-forward reproducible sin fuga de datos futuros.
- El comportamiento actual queda cubierto por pruebas deterministas, incluida la semilla semanal, la selección sin reemplazo y el fallback sin histórico.
- `Regenerar` produce otra candidata de la misma distribución y mantiene reproducible cada variación dentro de la sesión.
- La alternativa elegida documenta hipótesis, métricas, coste, limitaciones y criterio de abandono.
- La interfaz diferencia con claridad entre predicción validada y generación diversificada.
- La decisión final y sus resultados quedan registrados antes de implementar el nuevo generador.

**Dependencias:** histórico suficiente de sorteos ganadores; M-000 para la línea base de pruebas. La comparación futura con apuestas reales requiere empezar a persistir sus números, porque los seis meses anteriores no son reconstruibles a ese nivel.

**Progreso (2026-08-24):** iniciada la alternativa 1. Se han añadido el contrato y motor inicial del backtest, métricas comparables del modelo ponderado y una línea base uniforme, casos xUnit diseñados antes de la implementación, presentación de resultados en la página y variaciones explícitas para que `Regenerar` no reutilice siempre la combinación semanal. La comparación con la apuesta fija queda marcada como no disponible porque sus números históricos no se persistían. En esta entrega inicial quedaron pendientes las comprobaciones finales que se documentan en el cierre del hito.

**Resultado inicial (2026-08-24):** `scripts/Invoke-M702Backtest.ps1` evaluó en modo walk-forward 4.074 sorteos entre el 29 de octubre de 1987 y el 22 de agosto de 2026, después de reservar 104 sorteos iniciales para entrenamiento. El modelo ponderado obtuvo 3.067 coincidencias totales, media `0,752823`, máximo de 4 y 91 sorteos con al menos 3 aciertos; la línea base uniforme determinista obtuvo 3.062, media `0,751595`, máximo de 4 y 75 sorteos con al menos 3. Frente a la media uniforme teórica `0,734694`, el estadístico aproximado del modelo fue `z = 1,522576`, por debajo del umbral bilateral convencional `1,96`. **Conclusión provisional:** la pequeña diferencia observada no demuestra una ventaja predictiva estadísticamente convincente. Evidencia reproducible en `mejoras/evidencias/M-702-backtest-initial-20260824.json`; todavía deben evaluarse múltiples líneas base, periodos separados e hiperparámetros antes de decidir si conservar o abandonar el modelo.

**Comparación observada de seis meses:** entre el 24 de febrero y el 24 de agosto de 2026 existen 71 sorteos jugados con 71 € registrados tanto para la apuesta fija como para la automática. La fija obtuvo premio en 11 sorteos y acumuló 25 €; la automática obtuvo premio en 7 sorteos y acumuló 15 €. Por tanto, la automática no quedó literalmente sin premios, pero sí rindió peor que la fija en el periodo comunicado. Esta evidencia financiera no permite recalcular aciertos por número porque las combinaciones jugadas no se persistían.

**Comparación de las alternativas 2 y 3 (2026-08-24):** se evaluaron selección uniforme, modelo ponderado actual, cobertura diversificada y ensemble temporal/de pares con 5 apuestas por sorteo, 20.370 apuestas por estrategia y coste simulado idéntico. Uniforme obtuvo 368 premios principales; ponderado, 371; cobertura, 384; ensemble, 384. Ninguno produjo seis aciertos ni Especial; el ponderado generó el único caso de cinco aciertos. Las diferencias pareadas de cobertura (`z = 0,593769`) y ensemble (`z = 0,578812`) frente a uniforme quedaron muy por debajo de `±1,96`, por lo que ninguna demuestra ventaja predictiva. Informe en `mejoras/M-702_COMPARACION_ESTRATEGIAS.md`, evidencia en `mejoras/evidencias/M-702-strategy-comparison-20260824.json` y reproducción mediante `scripts/Invoke-M702StrategyComparison.ps1`.

**Nuevo algoritmo experimental (2026-08-24):** se añadió un ensemble adaptativo regularizado que mezcla online un experto uniforme y ventanas de 90, 365 y 1.825 días, actualiza sus pesos por pérdida Brier solo después de cada resultado, contrae la mezcla un 20 % hacia uniforme y diversifica cinco apuestas. Con el mismo backtest obtuvo 376 premios principales, máximo de 4 y `z = 0,303642` frente a uniforme: no mejoró a cobertura/ensemble ni demostró ventaja. El dato decisivo es que terminó asignando peso `0,99999139` al experto uniforme; el propio aprendizaje penalizó las tres señales históricas. Por tanto, tampoco existe base para presentarlo como predictor del premio mayor. El ensayo queda documentado como resultado exploratorio negativo y cualquier variante posterior deberá congelarse antes de una validación prospectiva.

**Decisión final para una apuesta semanal (2026-08-24):** adoptar selección uniforme sin reemplazo. Ningún modelo histórico demostró ventaja predictiva y el ensemble adaptativo terminó asignando `0,99999139` de peso al experto uniforme. La cobertura diversificada no aplica a una sola apuesta porque no existe cartera que optimizar. Tampoco se excluyen combinaciones ganadoras anteriores: `13-21-24-26-32-34` se repitió el 22 de agosto de 2002 y el 10 de diciembre de 2009, por lo que esa regla habría descartado una ganadora real sin mejorar la probabilidad individual.

**Implementación final (2026-08-24):** el generador de producción ya no consulta el histórico ni calcula frecuencias, decaimiento, suavizado o números calientes. Genera seis números distintos y equiprobables mediante barajado uniforme, mantiene la semilla semanal y una variación explícita para que `Regenerar` entregue otra candidata, y conserva el reintegro uniforme. La interfaz elimina la presentación bayesiana y los metadatos ponderados, identifica la selección uniforme adoptada y explica que todas las combinaciones válidas tienen la misma probabilidad. Se añadieron especificaciones xUnit para validez, determinismo, regeneración y ausencia de consulta histórica, además del verificador estático `scripts/Verify-M702UniformGenerator.ps1`. Las nuevas especificaciones xUnit no se ejecutaron porque la política del repositorio prohíbe compilar después de los cambios; esta limitación se mantiene explícita en el cierre.

**Iteración visual reversible (2026-08-24):** tras confirmar el usuario que `Regenerar` funciona correctamente, se rediseñó la página sin modificar la lógica validada. La propuesta concentra la combinación en un panel de alto contraste, convierte los números en bolas legibles y responsivas, separa el reintegro, muestra el ordinal de candidata, integra la explicación estadística y repliega el backtest como información secundaria. Se añadieron estados de foco, anuncio accesible durante la generación y adaptación móvil. La versión anterior quedó preservada en `mejoras/evidencias/M-702-ui-before-redesign/` para restaurarla exactamente si el usuario no aprobaba la propuesta. La valoración visual final se recoge en el cierre.

**Corrección visual tras revisión (2026-08-24):** la primera propuesta usó el verde fijo `#062f27` en vez de los tokens oficiales y una rejilla `aspect-square` que hacía crecer las bolas según el ancho disponible; dentro del panel recortado, su parte inferior y las acciones quedaban ocultas. Se sustituyó el fondo por el gradiente oficial `--brand-primary`/`--brand-secondary`, el contenedor dejó de contraerse dentro de la página y las bolas pasaron a tamaños responsivos acotados con distribución flexible. El verificador estático impide reintroducir el color fijo y la rejilla que causaba el recorte.

**Corrección del indicador de carga (2026-08-24):** el spinner dependía de la utilidad `animate-spin` suministrada en ejecución por Tailwind CDN y además aplicaba `motion-reduce:animate-none`, por lo que podía quedar completamente estático cuando el sistema o navegador anunciaba movimiento reducido. El indicador de generación usa ahora una animación CSS propia, continua y verificable; con movimiento reducido gira más despacio en lugar de detenerse. La animación decorativa del estado inicial conserva su comportamiento anterior y la lógica de generación no cambia.

**Recuperación del trébol visual (2026-08-24):** el usuario indicó que prefería conservar el trébol de cuatro hojas que decoraba cada número en la versión anterior. Se reincorporó dentro de cada bola circular como marca de agua discreta, usando el color oficial, por debajo del número y recortado por el propio círculo; se mantienen los tamaños acotados que corrigieron el desbordamiento. El verificador estático protege tanto su presencia como la legibilidad del número.

**Contraste del estado de regeneración (2026-08-24):** la revisión visual confirmó que el desenfoque transparente mantenía visibles grandes superficies blancas bajo el indicador y hacía que el spinner dorado y su texto se perdieran. Se adoptó un glass dorado derivado de `--brand-accent` para diferenciar el estado transitorio, pero no se colocó el spinner directamente sobre él: una tarjeta compacta de verde oscuro oficial proporciona contraste estable al arco dorado y al texto. Se incluyen colores de respaldo para navegadores sin `color-mix`, y la lógica de regeneración permanece intacta.

**Recuperación del mensaje de suerte (2026-08-24):** el usuario indicó que también prefería conservar la frase de ánimo mostrada tras generar una apuesta. Se recuperó el texto localizado exacto —«¡Mucha suerte con tu jugada! Que la fortuna te acompañe hoy.»— entre la combinación y sus acciones, destacando la segunda parte con el dorado oficial y sin alterar el proceso de generación.

**Ajuste final del glass de regeneración (2026-08-24):** la validación visual del usuario descartó el fondo glass dorado por resultar demasiado dominante. Se restauró el difuminado verde semitransparente, ahora definido explícitamente con los tokens `--brand-primary` y `--brand-secondary`, y se conservó la tarjeta verde oscura central porque resuelve por sí sola el contraste del spinner dorado y el texto. El verificador impide recuperar accidentalmente el overlay dorado descartado.

**Remate del mensaje de suerte (2026-08-24):** tras aprobar el conjunto visual, el usuario solicitó flanquear la frase de ánimo con dos tréboles. Se añadieron iconos vectoriales propios, dorados, simétricos y marcados como decorativos para accesibilidad; no se usan emojis dependientes de la plataforma y el texto localizado permanece intacto.

**Trébol aportado por el usuario (2026-08-24):** se sustituyeron los iconos provisionales del mensaje por el SVG `trebol-suerte.svg` seleccionado por el usuario. El original incluía un lienzo blanco de `2200 × 1466`, una versión monocroma adicional y metadatos de Illustrator; la copia autoalojada conserva únicamente el trébol verde coloreado, usa fondo transparente y reduce el archivo de 9.896 a 3.991 bytes. Se muestra dentro de dos soportes blancos simétricos para mantener contraste sobre el panel verde.

**Coherencia de bolas y reintegro (2026-08-24):** la revisión visual detectó que las bolas principales aún usaban el trébol provisional y que el reintegro se presentaba como una tarjeta naranja con cabecera en dos líneas, distinta y desalineada respecto a la combinación. Todas las bolas usan ahora `trebol-suerte.svg`; el reintegro comparte tarjeta blanca, cabecera horizontal, dimensiones circulares, jerarquía tipográfica y sombra con los números principales. El tono ámbar queda limitado al interior y borde de su bola para conservar la distinción semántica sin romper la alineación.

**Auditoría tipográfica (2026-08-24):** aunque `app.css` declaraba Poppins, el `<body>` aplicaba la clase Tailwind `font-['Inter',sans-serif]`, cuya especificidad anulaba la fuente base; además, el modal de reconexión y textos auxiliares de Chart.js conservaban Inter. Se eliminó la sobrescritura y la carga redundante, se unificaron esos casos explícitos y se carga Poppins en pesos `300–700`. La página de combinación no define una familia local y hereda ahora efectivamente `'Poppins', sans-serif` como el resto de la aplicación.

**Cierre y validación (2026-08-24):** el usuario comprobó en ejecución que `Regenerar` entrega candidatas nuevas y aprobó expresamente la interfaz final después de iterar color de marca, tamaños y alineación de bolas, reintegro, spinner animado con glass verde semitransparente, mensaje de suerte, trébol SVG aportado y tipografía Poppins. También se corrigieron los títulos de pestaña de Histórico y Combinación automática mediante `PageTitle` localizado. La lógica Razor de generación permaneció idéntica durante el rediseño visual.

**Pruebas de cierre:** `scripts/Verify-M702UniformGenerator.ps1` correcto; análisis sintáctico correcto de los tres scripts PowerShell M-702; `python -m unittest scripts.tests.test_m702_strategy_comparison` con 4/4 pruebas correctas; RESX y SVG válidos; SVG sin scripts ni referencias externas; bloque `@code` de `AutomatedCombination.razor` idéntico a la instantánea previa al rediseño; y `git diff --check` sin errores. Los casos xUnit de `AutomatedCombinationServiceTests` quedan añadidos pero **no ejecutados**, porque la guía del repositorio prohíbe compilar tras los cambios; no se presenta ninguna ejecución sobre binarios antiguos como validación de código nuevo.

**Resultado:** se cumplen los criterios funcionales y documentales de M-702 con una generación uniforme honesta, regeneración reproducible por variación, backtests walk-forward reproducibles y evidencia negativa publicada para los modelos predictivos. La validación manual del usuario completa el cierre visual y de interacción. **Commit o referencia:** `e2402f6`. No se inicia M-703.

**Versión del hito:** `1.1.0`, incremento minor compatible con SemVer porque M-702 añade y rediseña funcionalidad sin declarar una ruptura de contrato. `LaPrimitiva.App.csproj` fija la versión del ensamblado y el footer la obtiene en ejecución mediante `Assembly.GetName().Version`, sin duplicar un literal en la vista. El hito queda marcado por el tag anotado `v1.1.0`. **Commit o referencia:** commit de versión (este commit).

### [ ] M-703 — Persistir las apuestas realmente jugadas por sorteo

**Problema:** el registro conserva los costes y premios separados entre apuesta fija y automática, pero no guarda como datos estructurados las combinaciones que se jugaron en cada sorteo. La combinación fija solo dispone de `Plan.FixedCombinationLabel`, una etiqueta libre compartida por el plan, y las combinaciones automáticas generadas no quedan vinculadas al `DrawRecord` correspondiente. Las notas se están usando manualmente para suplir esa carencia, por lo que no existe una base fiable para comprobar aciertos ni reconstruir qué se jugó.

**Evidencia (2026-08-24):** en la captura aportada por el usuario, la columna Notas contiene textos como `A: 05 11 31 40 41 46 R: 05`. `DrawRecord` solo persiste importes y `Notes`; no contiene números, reintegro, Joker, origen de la apuesta ni ordinal cuando hay varias apuestas automáticas. M-702 ya confirmó que las combinaciones históricas no pueden reconstruirse a partir de los datos financieros actuales.

**Comportamiento esperado:**

- Cada sorteo jugado conserva una instantánea inmutable de todas sus apuestas: números, reintegro y Joker cuando corresponda.
- Cada apuesta se identifica por su origen (`Fija`, `Automática` u otro futuro) y por un ordinal estable cuando exista más de una del mismo tipo.
- Cambiar posteriormente un plan o regenerar una combinación no altera las apuestas ya asociadas a sorteos anteriores.
- El usuario puede revisar y corregir las combinaciones antes de confirmar el registro, sin tener que copiarlas a Notas.
- Los registros históricos sin combinación estructurada continúan siendo consultables y se muestran explícitamente como datos no disponibles, sin inventar ni inferir números desde las notas.

**Criterios de aceptación:**

- El modelo admite el número de apuestas configurado por `BetsPerDraw` y no presupone que siempre exista una única fija y una única automática.
- Se validan seis números distintos entre 1 y 49, reintegro entre 0 y 9 y el formato de Joker aplicable.
- Existen pruebas de persistencia, edición previa a confirmación, múltiples apuestas automáticas e inmutabilidad frente a cambios posteriores del plan.
- La generación automática de M-702 puede entregar su candidata al registro sin transcripción manual.
- La migración y la interfaz mantienen compatibilidad explícita con sorteos históricos que solo tienen importes y notas.

**Dependencias:** M-401 para ejecutar el cambio de esquema mediante migración EF Core; coordinación con M-702 para capturar la combinación automática generada.

### [ ] M-704 — Detectar y notificar automáticamente las apuestas premiadas

**Problema:** al guardar desde el popup el resultado oficial de un sorteo, la aplicación únicamente confirma que se añadió al histórico. El usuario debe comparar manualmente los números oficiales con las apuestas de esa semana, identificar cuál obtuvo premio y escribirlo en Notas. Este flujo es lento, propenso a errores y oculta una de las conclusiones más valiosas de la aplicación.

**Evidencia (2026-08-24):** `MainLayout.SaveToHistory()` llama a `WinningDrawService.SaveFromRssAsync()` y muestra el mensaje genérico `Sorteo guardado correctamente en el histórico.`. El resultado RSS sí contiene los seis números, complementario, reintegro y Joker, pero el flujo no busca el `DrawRecord` de la misma fecha ni evalúa sus apuestas. La captura del Registro muestra que hoy la identificación de la combinación jugada se mantiene manualmente en Notas.

**Comportamiento esperado:**

- Después de guardar o corregir un resultado oficial, la aplicación cruza todas las apuestas estructuradas del sorteo de la misma fecha y plan.
- Por cada apuesta premiada indica de forma inequívoca cuál fue (`Fija`, `Automática 1`, `Automática 2`, etc.), sus aciertos y la categoría obtenida, incluidos reintegro y Joker cuando correspondan.
- El resultado aparece en una alerta de éxito y queda accesible en el panel derecho como notificación persistente hasta que el usuario la revise.
- Si no hay premio, se informa de que la comprobación terminó sin coincidencias premiadas; si faltan las combinaciones jugadas, se informa de que no fue posible comprobarlas automáticamente.
- Repetir la comprobación o editar el resultado oficial es idempotente: actualiza la liquidación existente y no duplica alertas ni premios.
- La detección de categoría no modifica automáticamente los importes económicos mientras la fuente RSS no proporcione una tabla de premios verificable; el usuario conserva el control de los importes o estos se obtienen de una fuente oficial separada en un hito futuro.

**Criterios de aceptación:**

- Las reglas de categorías están aisladas en un evaluador de dominio y cubiertas con casos límite: seis aciertos, cinco más complementario, cinco, cuatro, tres, reintegro, Joker y ausencia de premio.
- Existen pruebas con varias apuestas ganadoras en un mismo sorteo y con varios planes aplicables a la fecha.
- El cruce usa una identidad estable de sorteo y apuesta, no texto libre de Notas.
- Guardar desde el popup, crear manualmente y corregir un resultado ejecutan la misma lógica de evaluación.
- La notificación enlaza o permite navegar al registro concreto y muestra el estado incluso después de recargar la aplicación.
- La verificación manual confirma los tres estados: con premio, sin premio y no comprobable por falta de datos históricos.

**Dependencias:** M-703; M-701 para que todos los sorteos RSS pendientes puedan guardarse en cualquier orden.

### [ ] M-705 — Rediseñar el Registro para distinguir inversión, premios y balance

**Problema:** la tabla de escritorio presenta en una única línea muchas columnas con abreviaturas (`C. Fija`, `P. Auto`, `Total C.`, `Total P.`), pesos visuales muy parecidos y una columna de notas dominante. Aunque los datos existen, no se distingue de un vistazo qué se jugó, qué se ganó ni cuál fue el resultado neto; los premios nulos y los sorteos premiados reciben casi el mismo tratamiento visual.

**Evidencia (2026-08-24):** la captura aportada del Registro muestra costes y premios en bloques contiguos con diferencias de fondo muy sutiles, valores `0,00 €` repetidos y notas truncadas ocupando gran parte de la fila. `Register.razor` usa encabezados abreviados y solo aplica el color positivo o negativo a Neto y Acumulado; la fila no comunica si contiene una apuesta premiada y depende demasiado del color para interpretar el resultado.

**Comportamiento esperado:**

- La tabla agrupa visual y semánticamente tres bloques: `Jugado`, `Ganado` y `Balance`, con encabezados completos o ayuda contextual para las abreviaturas.
- Los sorteos con premio se reconocen de inmediato mediante una insignia y jerarquía visual que no dependa solo del color; los importes cero quedan atenuados.
- La identidad de la apuesta premiada y su categoría se muestran como resumen estructurado, mientras Notas queda como información secundaria expandible.
- Neto y acumulado conservan protagonismo, pero se diferencia claramente el resultado del sorteo del balance acumulado del plan.
- En pantallas estrechas se usa una representación adaptada —tarjetas, detalle expandible o columnas prioritarias— en lugar de comprimir toda la tabla.

**Criterios de aceptación:**

- Una persona puede identificar en una revisión visual qué se jugó, qué se ganó y el balance sin abrir el formulario de edición.
- La información crítica dispone de texto, icono o etiqueta además del color y mantiene contraste y navegación por teclado adecuados.
- Existen estados visuales diferenciados para no jugado, jugado sin premio, premiado, pendiente de comprobar y no comprobable.
- Se verifican al menos escritorio, tableta y móvil con datos largos, múltiples apuestas premiadas y notas extensas.
- Los cálculos y datos persistidos no cambian como consecuencia del rediseño; se repiten las verificaciones afectadas de M-203 y la parte correspondiente de la Fase 6.

**Dependencias:** M-703 y M-704 para mostrar apuestas y categorías estructuradas; puede prototiparse antes, pero su cierre requiere esos datos.

### [ ] M-706 — Incorporar un ciclo de cierre auditado para cada sorteo

**Problema:** el registro no expresa si un sorteo está esperando el resultado oficial, si ya fue comprobado, si tiene un premio cuyo importe todavía debe registrarse o si está completamente cerrado. La existencia de números, notas o importes no basta para distinguir esos estados, de modo que un sorteo puede quedar incompleto sin que la aplicación lo advierta.

**Evidencia (2026-08-24):** `DrawRecord` persiste `Played`, costes, premios, notas y marcas de creación/actualización, pero no conserva un estado de conciliación ni evidencia de cuándo y contra qué resultado oficial se comprobó. `Register.razor` permite editar importes directamente y no diferencia entre un cero confirmado y un premio todavía pendiente de registrar.

**Comportamiento esperado:**

- Cada sorteo muestra un estado inequívoco: `Pendiente de resultado`, `Pendiente de comprobación`, `Premio pendiente de importe`, `Cerrado` o `No comprobable`.
- Las transiciones normales se producen a partir de hechos verificables —resultado oficial disponible, apuestas persistidas y liquidación calculada— y no de texto libre.
- El cierre conserva la fecha de comprobación, la identidad del resultado oficial utilizado y la procedencia de los importes registrados.
- Corregir un resultado oficial o una apuesta reabre de forma controlada el sorteo afectado y obliga a repetir la conciliación.
- Los registros históricos incompletos se clasifican como `No comprobable` o pendientes, sin asumir que un importe cero significa necesariamente ausencia de premio.

**Criterios de aceptación:**

- Existe una máquina de estados o regla de transición explícita, validada en dominio y compartida por todos los flujos de guardado.
- No se puede marcar como `Cerrado` un sorteo jugado sin resultado oficial o sin una comprobación concluyente, salvo excepción manual justificada y auditada.
- Las transiciones son idempotentes y conservan fecha, causa y estado anterior cuando se reabre un sorteo.
- Existen pruebas para el recorrido completo, corrección posterior, premio pendiente, sorteo no jugado y datos históricos no comprobables.
- La interfaz explica por qué un sorteo está pendiente y cuál es la siguiente acción necesaria.

**Dependencias:** M-703 y M-704; coordinación con M-403 para evitar cierres perdidos ante ediciones concurrentes.

### [ ] M-707 — Crear un centro de tareas pendientes y revisión

**Problema:** las acciones que requieren intervención están repartidas entre el popup RSS, el histórico y el registro. El usuario debe recordar qué resultados faltan, qué apuestas no pueden comprobarse y qué premios necesitan importe, sin disponer de una cola única de trabajo.

**Evidencia (2026-08-24):** el panel derecho de `MainLayout.razor` muestra resultados RSS pendientes de guardar, pero no agrega incidencias del Registro. Tras `SaveToHistory()` actualiza las notificaciones y los datos globales, sin presentar tareas de conciliación, premios pendientes ni sorteos incompletos.

**Comportamiento esperado:**

- El panel derecho incorpora una sección `Pendientes` con contadores y tareas accionables para resultados oficiales, apuestas sin registrar, comprobaciones pendientes, premios sin importe y registros no comprobables.
- Cada tarea explica el motivo, muestra fecha, plan y prioridad, y navega directamente al registro o acción capaz de resolverla.
- Las tareas desaparecen automáticamente cuando se resuelve su causa y no pueden descartarse de forma que oculte una inconsistencia real.
- El usuario puede filtrar por tipo y plan, marcar una notificación informativa como vista y distinguir `requiere acción` de `solo información`.
- El panel mantiene el comportamiento acotado y desplazable ya validado para las notificaciones RSS.

**Criterios de aceptación:**

- Los contadores se derivan del estado persistido y coinciden con los registros que aparecen al abrir cada filtro.
- Existe al menos una prueba por tipo de tarea y una prueba de resolución que verifica su desaparición sin recargar manualmente.
- Una misma causa produce una sola tarea estable aunque se repita la sincronización o se abra la aplicación varias veces.
- Las consultas permanecen acotadas y no cargan todo el histórico para calcular el panel.
- La verificación manual cubre panel vacío, varias clases de pendientes, navegación profunda y uso en móvil.

**Dependencias:** M-701 y M-706; reutiliza el panel derecho evolucionado en M-204.

### [ ] M-708 — Obtener y conciliar los importes oficiales por categoría

**Problema:** M-704 puede determinar la categoría obtenida comparando números, pero el RSS actual no proporciona una tabla verificable con el importe de cada premio. Introducir manualmente esos importes mantiene una parte sensible del proceso expuesta a errores de transcripción.

**Evidencia (2026-08-24):** `RssDraw` y `WinningDrawDto` contienen números, complementario, reintegro y Joker, pero no premios por categoría. La página oficial mostrada por el usuario publica una tabla de categorías, acertantes e importes; esa información no forma parte del flujo actual de `WinningDrawService.SaveFromRssAsync()`.

**Enfoque y alternativas:**

1. **Fuente oficial estructurada — opción preferida:** consumir una API, feed o recurso oficial estable y versionado si está disponible.
2. **Adaptador HTML aislado:** extraer la tabla de la página oficial solo si no existe una fuente estructurada, con límites de descarga, fixtures capturados y detección explícita de cambios de formato. Es más frágil y exige mantenimiento.
3. **Confirmación manual asistida:** presentar la categoría detectada y solicitar únicamente el importe cuando la fuente oficial no esté disponible. Mantiene el flujo operativo sin inventar datos.

**Comportamiento esperado:**

- Cada importe conserva categoría, cantidad, moneda, fuente oficial, instante de obtención y versión o huella de los datos usados.
- La aplicación propone los importes correspondientes a las categorías detectadas, pero muestra su procedencia antes de confirmar la liquidación.
- Una fuente ausente, incompleta o con formato desconocido no asigna importes silenciosamente: deja la tarea pendiente y activa el flujo manual asistido.
- Las correcciones oficiales vuelven a conciliar únicamente los sorteos afectados y mantienen trazabilidad del valor anterior.
- Los redondeos y categorías especiales, incluidos reintegro y Joker, se tratan mediante reglas explícitas y verificables.

**Criterios de aceptación:**

- Antes de elegir el adaptador se documenta y verifica la fuente oficial disponible, sus condiciones de uso, estabilidad y cobertura histórica.
- El parser se prueba con fixtures reales de sorteos con y sin acertantes, botes, importes cero, reintegro y Joker.
- Un cambio inesperado de esquema genera una alerta diagnóstica y nunca transforma texto dudoso en dinero.
- La liquidación económica es idempotente y diferencia importe propuesto, confirmado y corregido.
- Se conserva siempre una vía manual explícita y auditada.

**Dependencias:** M-304 para límites de descarga y parseo; M-704 y M-706 para categoría y estado de conciliación.

### [ ] M-709 — Comparar el rendimiento real de las estrategias jugadas

**Problema:** el dashboard resume gasto, premios y efectividad global, pero no permite comparar de manera justa la combinación fija con las automáticas ni explicar qué estrategia produjo cada categoría. Las comparaciones manuales por periodos pueden mezclar distinto número de apuestas, costes o sorteos y conducir a conclusiones engañosas.

**Evidencia (2026-08-24):** `SummaryService` agrega importes fijos y automáticos y M-702 cuantificó seis meses de resultados financieros, pero las combinaciones históricas no estaban persistidas. No existe una vista que alinee estrategias por las mismas fechas, presupuesto y número de apuestas, ni que muestre cobertura de datos faltantes.

**Comportamiento esperado:**

- La aplicación compara fija y automáticas sobre el mismo plan, intervalo y conjunto de sorteos jugados, mostrando cuándo la comparación no es homogénea.
- Se presentan coste, premios, neto, ROI, sorteos con premio, categorías obtenidas, mejor/peor racha y evolución acumulada por estrategia.
- Las métricas separan resultados económicos reales de los backtests simulados de M-702 y no presentan diferencias observadas como capacidad predictiva demostrada.
- Cada informe indica cobertura, sorteos excluidos y proporción de registros sin combinaciones estructuradas.
- El usuario puede alternar vista temporal, acumulada y por categoría sin perder el presupuesto comparable.

**Criterios de aceptación:**

- Todas las estrategias se comparan con igual coste simulado o se normalizan y etiquetan de forma explícita; nunca se enfrentan totales incomparables sin advertencia.
- Los cálculos se contrastan contra casos manuales pequeños y contra los agregados financieros ya existentes.
- Existen pruebas con periodos parciales, varias apuestas automáticas, planes diferentes, sorteos no jugados y datos históricos incompletos.
- El informe distingue evidencia descriptiva, simulación retrospectiva y cualquier prueba estadística aplicada.
- La interfaz evita lenguaje que sugiera predicción o causalidad no demostrada.

**Dependencias:** M-702 para metodología de comparación; M-703 y M-704 para apuestas y categorías estructuradas.

### [ ] M-710 — Añadir búsqueda, filtros y vistas operativas al Registro

**Problema:** al crecer el histórico, seleccionar únicamente año y plan obliga a recorrer visualmente una tabla extensa para localizar premios, categorías o registros pendientes. El rediseño de M-705 mejora la lectura de cada fila, pero no resuelve la localización de subconjuntos relevantes.

**Evidencia (2026-08-24):** `Register.razor` permite seleccionar año y plan, mientras que el listado no ofrece búsqueda ni filtros por estado, estrategia, categoría o presencia de premio. Las notas se truncan y no existe una vista rápida `Solo premiados`.

**Comportamiento esperado:**

- El Registro permite filtrar por estado de conciliación, jugado/no jugado, con/sin premio, estrategia ganadora, categoría, rango de fechas y plan.
- Incluye accesos rápidos como `Solo premiados`, `Pendientes` y `No comprobables`, sincronizados con el centro de tareas.
- La búsqueda localiza fecha, semana y texto de notas sin confundir números de apuesta con importes.
- Los filtros activos son visibles, pueden limpiarse individualmente y se conservan al navegar al detalle y regresar.
- Los resultados muestran el número de coincidencias y un estado vacío que explica qué filtros están excluyendo datos.

**Criterios de aceptación:**

- La combinación de filtros usa semántica documentada y produce resultados deterministas tanto en escritorio como en móvil.
- Los filtros frecuentes se resuelven mediante consultas proyectadas y acotadas, sin cargar todo el histórico en memoria.
- La navegación desde una tarea pendiente abre el Registro con los filtros correctos y permite volver sin perder contexto.
- La exportación, si se ofrece desde la vista filtrada, indica claramente si exporta el conjunto visible o todo el año.
- Existen pruebas para combinaciones de filtros, parámetros inválidos, estado vacío y persistencia del contexto de navegación.

**Dependencias:** M-705 para la nueva presentación; M-706 y M-707 para estados y navegación desde tareas; M-704 para categorías estructuradas.

### [ ] M-711 — Paginar la tabla del Registro y seleccionar el tamaño de página

**Problema:** la tabla del Registro crece continuamente y actualmente carga y renderiza todos los sorteos del año y plan seleccionados en una sola vista. Esto aumenta el desplazamiento, dificulta localizar el punto de trabajo y hará que el coste de consulta y renderizado crezca con el histórico.

**Evidencia (2026-08-24):** `Register.LoadData()` obtiene la lista completa mediante `DrawRepository.GetListAsync(...)`, filtra el plan en memoria y construye `drawsWithCumulative` con todos los resultados ordenados. Las vistas de escritorio y móvil recorren esa colección completa con `foreach`; no existen tamaño de página, página actual, recuento total ni controles de navegación.

**Comportamiento esperado:**

- Debajo de la tabla se muestra una paginación con página anterior, siguiente, primera, última y páginas cercanas a la actual.
- El usuario puede seleccionar `10`, `25`, `50` o `100` registros por página; el valor inicial recomendado es `25`.
- Se informa del intervalo visible y del total, por ejemplo: `Mostrando 26–50 de 184 registros`.
- Cambiar año, plan, filtros o tamaño de página vuelve a la primera página; regresar desde el detalle conserva la página cuando el conjunto no ha cambiado.
- Si una eliminación deja la página actual vacía, la vista retrocede automáticamente a la última página válida.
- La ordenación es estable —fecha descendente y un segundo criterio determinista— para que un registro no salte entre páginas con fechas coincidentes.
- El acumulado mostrado conserva el valor real dentro del plan y periodo completo; no se reinicia al comienzo de cada página.

**Decisión técnica recomendada:** implementar paginación en la consulta —recuento total más proyección ordenada con `Skip`/`Take` o equivalente— en lugar de cargar todo el histórico y recortarlo únicamente en la interfaz. La paginación en memoria sería más sencilla, pero no resolvería el crecimiento del coste de acceso a datos.

**Criterios de aceptación:**

- Los tamaños disponibles son exactamente `10`, `25`, `50` y `100`, sin aceptar valores arbitrarios o manipulados.
- El número de páginas se calcula a partir del total filtrado y ningún registro se duplica ni se omite al recorrerlas con un conjunto de datos estable.
- La paginación se aplica después de los filtros de M-710 y el total refleja el mismo conjunto filtrado.
- Existen pruebas para cero registros, una página exacta, una página parcial, cambio de tamaño, página fuera de rango y eliminación del último elemento de una página.
- Los controles tienen etiquetas accesibles, estado deshabilitado correcto y funcionamiento mediante teclado; en móvil no provocan desbordamiento horizontal.
- La consulta recupera únicamente la página solicitada y los datos mínimos necesarios para calcular totales y acumulados sin materializar todo el histórico de entidades.
- La verificación manual recorre todos los tamaños y confirma que el acumulado de un mismo sorteo coincide independientemente de la página seleccionada.

**Dependencias:** M-710 para componer paginación y filtros sobre una única consulta; coordinación con M-705 para integrar los controles en las vistas de escritorio y móvil. Puede implementarse antes de esos hitos si se conserva el contrato de integración.

**Criterio de cierre del plan:** todas las fases y todos los hitos emergentes aplicables están completados, verificados y asociados a evidencia reproducible.

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
| M-103 | 2026-08-20 | Commit `M-103` (este commit), sobre `d48abb7` | Completado | Base única por ejecución, migraciones, reset Respawn, borrado protegido, colección serializada y CSV portable; verificación estática doble correcta. |
| M-201 | 2026-08-20 | Commit `M-201` (este commit), creado tras M-103 | Completado | Actualización explícita de sorteos desconectados con entidad seguida y lista blanca de columnas; prueba de integración añadida, build correcto y verificación visual satisfactoria. |
| M-202 | 2026-08-20 | Commit `M-202` (este commit), sobre `4a7cfdf` | Completado | Ruta `/registro` centralizada en `AppRoutes.Registration`; verificador estático correcto y navegación Planes → Registro comprobada manualmente por el usuario. |
| M-203 | 2026-08-20 | Commit `M-203` (este commit), sobre `07a58cc` | Completado | Totales de coste y premios incluyen los cuatro componentes; persistencia y reparación aplican el invariante; casos Joker y ROI añadidos; build, ejecución y comprobación visual satisfactorios comunicados por el usuario. |
| M-205 | 2026-08-20 | `32d7876`, corrección `e315855` | Completado | Validación coherente en UI, Application, dominio, repositorio y SQL; `BetsPerDraw` aplicado a costes; verificación estática correcta, 14 casos xUnit añadidos y guardado de edición comprobado en ejecución por el usuario. |
| M-206 | 2026-08-24 | Commit de cierre sobre `373561a`; release `v1.2.0` | Completado | Actualización con entidad seguida y lista blanca; `CreatedAt` preservado y `UpdatedAt` renovado; límites coincidentes rechazados al crear y editar; selector `Todos` validado en ejecución; footer de versión corregido y comprobado visualmente. |
| M-702 | 2026-08-24 | `e2402f6`, `19defe6`, release `373561a`, tag `v1.1.0` | Completado | Generación uniforme y rediseño validados; títulos de Histórico y Combinación automática unificados con `PageTitle`; footer enlazado a la versión del ensamblado y release `1.1.0` publicada. Verificación estática correcta y validación visual del usuario; casos xUnit nuevos no ejecutados por la prohibición de compilar. |
