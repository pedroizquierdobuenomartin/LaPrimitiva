# M-601 — Verificación funcional completa

## Alcance y regla de atribución

Esta verificación cubre la revisión `c5d76a6` y los cambios exclusivos de M-601. Se usan tres señales separadas:

1. **Suite fresca:** `dotnet test --solution .\LaPrimitiva.sln` compila y ejecuta todas las pruebas unitarias y de integración contra SQL Server.
2. **Recorrido funcional:** navegador real sobre la aplicación compilada para la revisión, conectado únicamente a `PrimitivaAuditV2_M601Tests`.
3. **Comprobación estática:** `scripts/Verify-M601FunctionalVerification.ps1` y `git diff --check`; no sustituyen a las dos señales anteriores.

Una ejecución `--no-build` sobre binarios anteriores nunca acredita los cambios de M-601. La base de desarrollo `PrimitivaAuditV2` queda fuera de alcance.

## Evidencia previa reproducible — 2026-08-31

El repositorio partió limpio en `main`, sincronizado `0/0` con `origin/main`, en `c5d76a6`. La suite fresca contra `localhost\LOCALSERVER` ejecutó 177 pruebas: **145 correctas y 32 fallidas**.

- 27 fallos esperaban `InvalidOperationException` pese a que M-506 ya tipó esas fronteras con `BusinessRuleException` o `DataIntegrityException`.
- Dos pruebas estáticas seguían buscando el literal `Referencia:` en Razor después de que M-507 lo trasladara a `GlobalResource`.
- La prueba HTTP de `/Error` recibía `403` porque `TestServer` no aporta una IP remota y la fixture no inyectaba loopback.
- Al abrir `/` en un navegador real se obtuvo HTTP 500. `SecureJsonFileLoggerProvider` intentaba serializar `System.RuntimeType` desde `Metadata.TypeId`; el `NotSupportedException` del sink se propagaba y derribaba la petición.

La base aislada `PrimitivaAuditV2_M601Tests` se creó mediante las migraciones administrativas. No se creó ningún certificado: el paquete HTTPS transferible continúa siendo el definido por M-306 en `artifacts/local-https`.

## Correcciones incorporadas en M-601

- El sink JSON normaliza propiedades y scopes a valores JSON seguros, limita colecciones y convierte `Type` a su nombre; una incompatibilidad residual del serializador no puede romper la petición.
- Las pruebas de dominio, aplicación, repositorio e integración esperan la taxonomía estable de M-506.
- Las pruebas de presentación comprueban las claves de recursos de M-507 en vez de reintroducir literales españoles en Razor.
- La fixture HTTP inserta `IPAddress.Loopback` mediante un `IStartupFilter` exclusivo del host de integración; la política real no se relaja.
- Las rutas `/Error`, `/404` y `/not-found` disponen de etiquetas localizadas en el breadcrumb; una clave ausente ya no convierte la propia página de error en otro error del boundary.
- Las pruebas HTTP decodifican entidades HTML antes de comparar textos localizados; la codificación segura de tildes por Razor no se interpreta como ausencia de traducción.
- Los planes finitos futuros conservan su año inicial en el selector global; el límite defensivo ya no puede recortar un intervalo por debajo de `EffectiveFrom`.
- El alta semanal muestra apuestas y Joker desde el plan seleccionado antes de crear los sorteos, en vez de presentar provisionalmente `0` y `NO`.
- La exportación recalcula el neto acumulado en orden cronológico por plan y año, igual que Registro, evitando exportar el `0,00` persistido como valor obsoleto.
- El icono móvil de Registro usa un path SVG válido y deja de generar errores de consola.

## Ejecución automatizada final

```powershell
$env:LAPRIMITIVA_INTEGRATION_TEST_CONNECTION = 'Server=localhost\LOCALSERVER;Database=PrimitivaAuditV2_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True;Encrypt=False'
dotnet test --solution .\LaPrimitiva.sln
.\scripts\Verify-M601FunctionalVerification.ps1
git diff --check
```

## Datos conocidos para la comprobación de cálculos

El caso reproducible usa un plan con coste base `1,25`, tres apuestas, Joker habilitado y coste Joker `0,50`. Al marcar un sorteo como jugado se esperan:

| Concepto | Fija | Automática | Total |
|---|---:|---:|---:|
| Coste base | 1,25 | 2,50 | 3,75 |
| Coste Joker | 0,50 | 1,00 | 1,50 |
| Coste completo | 1,75 | 3,50 | **5,25** |

Con premios fija `5,00`, automática `3,00`, Joker fija `20,00` y Joker automática `10,00`, el premio total es **38,00** y el neto es **32,75**. Dashboard y exportación deben mostrar exactamente esos totales; el ROI matemático es `32,75 / 5,25 × 100 = 623,81 %` y el Dashboard lo presenta como `623,8 %` a un decimal.

## Matriz de recorrido funcional

Usar el año 2036, que parte sin planes ni registros. Guardar para cada flujo resultado, dato observado y, si falla, referencia de error.

| ID | Recorrido y criterio | Resultado |
|---|---|---|
| FLOW-PLANES | Crear `M601 2036` (01/01–31/12, 3 apuestas, costes anteriores), recargar, renombrar y confirmar persistencia. | **APTO**: el binario fresco conservó el plan `M601 final 2036`; tras recargar, el selector incluyó 2036 y mostró el plan con 3 apuestas y Joker activo. |
| FLOW-REGISTRO | Crear el sorteo del jueves de la primera semana, marcarlo jugado, recargar y confirmar el estado. | **APTO**: antes de preparar la semana, la cabecera mostró `3 Apuestas por sorteo • Joker SÍ`; el 10/01/2036 persistió como jugado. |
| FLOW-PREMIOS | Registrar los cuatro premios conocidos y confirmar total `38,00` y neto `32,75` tras recargar. | **APTO**: `38,00`, `32,75` y acumulado visual `32,75` tras guardar. |
| FLOW-JOKER | Confirmar costes Joker `0,50 + 1,00`, premios `20,00 + 10,00` y total completo `5,25`. | **APTO**: los cuatro importes y el total coincidieron. |
| FLOW-DASHBOARD | Seleccionar 2036 y contrastar gasto `5,25`, ganado `38,00`, neto `32,75` y ROI. | **APTO**: `5,25`, `38,00`, `32,75` y `623,8 %`; histórico global devolvió los mismos totales. |
| FLOW-HISTORICO | Crear un resultado temporal, editar su complementario, recargar y eliminarlo. | **APTO**: 31/08/2026 con `1..6`, Joker `0123456`; complementario `7→9`; borrado confirmado. |
| FLOW-RSS | Comprobar que el feed conocido se procesa sin bloquear la UI y que un resultado ya existente no se duplica. | **APTO**: el binario final mostró `¡Todo al día!` y `No hay sorteos nuevos pendientes de registro`; los resultados ya existentes quedaron filtrados y no se ofreció una segunda acción de guardado. |
| FLOW-EXPORTACION | Exportar y contrastar cabecera, una fila jugada, cultura de contrato y los totales conocidos. | **APTO**: `LaPrimitiva-Export-20260831.csv` contuvo una fila con coste `5.25`, premios `38.00`, neto `32.75` y acumulado `32.75`. |
| FLOW-GENERACION | Generar y regenerar; cada resultado contiene seis números únicos `1..49` y reintegro `0..9`. | **APTO**: candidata `1, 8, 11, 15, 24, 41`, todos únicos, reintegro `4`. |
| FLOW-CRUD-LIMPIEZA | Eliminar los registros creados y el plan solo cuando no tenga sorteos asociados; confirmar ausencia. | **APTO**: semana, resultado histórico y plan eliminados; el 01/09/2026 se repitió la limpieza final y, tras recargar, 2036 dejó de aparecer en el selector. |

## Cierre dirigido — 2026-08-31 / 2026-09-01

- El usuario ejecutó un build fresco y `dotnet test --solution .\LaPrimitiva.sln`: **182/182 pruebas correctas**, 0 errores y 0 omitidas, en 9,725 s.
- Sobre ese binario, conectado exclusivamente a `PrimitivaAuditV2_M601Tests`, se repitieron las cinco señales afectadas: selector 2036, cabecera `3 Apuestas/Joker SÍ`, CSV con acumulado `32.75`, consola sin errores y filtrado idempotente del RSS.
- La consola del navegador solo registró mensajes informativos de conexión Blazor; no se observaron errores ni advertencias SVG.
- Los datos temporales se eliminaron por la UI. Tras recargar, el selector volvió a contener únicamente `Todos` y `2026`.
- No se creó ningún certificado durante M-601. La decisión de M-306 de conservar el paquete HTTPS descargable y transferible en `artifacts/local-https` permanece intacta.
- Las correcciones compatibles se publican como versión patch `v1.17.1`.

## Estado

**APTO.** M-601 queda verificado funcionalmente sobre la fuente actual. La suite fresca, el recorrido real, los cálculos conocidos, el CRUD aislado y la limpieza final coinciden; no se avanzó a M-602.
