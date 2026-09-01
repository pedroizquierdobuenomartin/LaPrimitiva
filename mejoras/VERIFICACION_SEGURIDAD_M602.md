# Verificación de seguridad M-602

**Fecha:** 2026-09-01

**Estado:** APTO

**Referencia fuente:** `b66611604b0a9e3587da7391813afde56acd95cd` (`v1.17.1`)

**Codex Security scan:** `95f84eb6-0417-4317-8a9b-6b32fab35ad3`

**Evidencia reproducible:** `mejoras/evidencias/M-602-security-verification-20260901.json`

## 1. Estado previo y problema confirmado

Antes de editar, `main` estaba limpio, sincronizado con `origin/main` y apuntaba a `b666116` (`v1.17.1`). M-602 seguía sin marcar y el repositorio no contenía un verificador ni una evidencia consolidada que repitiera conjuntamente el análisis estático, los avisos online, la escucha runtime, RSS, CSV y CSP.

La brecha confirmada era de **verificación y trazabilidad**, no una vulnerabilidad ya demostrada. Los controles de M-301, M-302, M-304 y M-305 seguían presentes, pero solo tenían verificadores separados y evidencia histórica.

## 2. Modelo y alcance

Se revisaron las fronteras que pueden aportar una capacidad nueva a datos no confiables:

| Frontera | Activo | Control esperado |
|---|---|---|
| Navegador/proceso local → ASP.NET Core | Datos y acciones de la aplicación | Binding y clientes exclusivamente loopback |
| SELAE → cliente/parser RSS | Disponibilidad e integridad del histórico | URL fija, 512 KiB, 100 items, 15 s, cancelación, sin DTD |
| Servidor → navegador | Integridad del código ejecutado | Assets autoalojados, versiones fijadas y CSP estricta |
| Notas → CSV → hoja de cálculo | Equipo que abre la exportación | Neutralización de `=`, `+`, `-`, `@` y escaping CSV |
| NuGet/npm → build/runtime | Cadena de suministro | Cero advisories conocidos, incluidas transitivas |

El modelo sigue siendo local y monousuario. No se presupone resistencia frente a un administrador del propio equipo ya comprometido. La configuración IIS instalada es estado externo al repositorio; la prueba runtime de esta ejecución cubrió el binario Kestrel actual y el rechazo fail-fast de configuraciones no loopback.

## 3. Resultados

| Comprobación | Evidencia | Resultado |
|---|---|---|
| Análisis estático estándar | Scan `95f84eb6-0417-4317-8a9b-6b32fab35ad3` sobre `b666116`: 6 superficies cerradas, 0 hallazgos confirmados. Revisión secuencial degradada, sin auditor independiente; bundles minificados y fuentes se validaron por hash, versión y advisories, no línea por línea. El worktree cambió durante el scan únicamente al añadir estos artefactos M-602; el objetivo quedó congelado en el código de producto de `b666116`. | **APTO con limitaciones explícitas** |
| APIs peligrosas en producto | Sin SQL raw, ejecución de procesos, deserialización binaria insegura, DTD habilitado, `eval`, `new Function` ni escritura mediante `innerHTML`. La URL RSS es constante. | **APTO** |
| Dependencias NuGet | `dotnet package list --project LaPrimitiva.sln --vulnerable --include-transitive --no-restore --format json`, consultando `https://api.nuget.org/v3/index.json`. | **0 vulnerabilidades** |
| Dependencias npm | `npm audit --json`, incluidas dependencias de producción y desarrollo: 77 dependencias totales. | **0 vulnerabilidades** |
| Binding no loopback | Arranque del binario existente con `--urls http://0.0.0.0:<puerto>`: termina antes de construir el servidor con `InvalidOperationException` de `LocalOnlyPolicy`. | **Rechazado correctamente** |
| Listener loopback | Arranque con `127.0.0.1:<puerto>`; `netstat` detectó un único `LISTENING` en esa dirección y `/health/live` respondió HTTP 200. | **APTO** |
| RSS | `Verify-M304RssLimits.ps1`; streaming limitado, rechazo por `Content-Length` y por cuerpo real, máximo 100 items, timeout, cancelación y exclusión mutua. | **APTO** |
| CSV | `Verify-M305CsvFormulaNeutralization.ps1`; los cuatro prefijos de fórmula se neutralizan y las notas pasan por `CsvFieldFormatter`. | **APTO** |
| CSP y scripts | `Verify-M302ContentSecurity.ps1`, búsqueda global de scripts/imports remotos en fuentes web manuscritas y cabeceras runtime. CSP: `script-src 'self'`, sin `unsafe-inline`, `unsafe-eval` ni comodines; `nosniff` y `no-referrer`. | **APTO** |

## 4. Pruebas ejecutadas

Antes de crear artefactos M-602:

- `Verify-M301LocalOnly.ps1`, `Verify-M302ContentSecurity.ps1`, `Verify-M304RssLimits.ps1` y `Verify-M305CsvFormulaNeutralization.ps1`: correctos.
- Ejecución focalizada del binario de pruebas existente para `LocalOnlySecurityTests`, `SecurityHeadersMiddlewareTests`, `RssClientTests`, `RssParserServiceTests`, `CsvFieldFormatterTests` y `CsvExportBuilderTests`: **33/33 correctas**.
- La suite completa `dotnet test --solution LaPrimitiva.sln --no-build --no-restore` ejecutó 199 casos: 162 correctos y 37 errores de integración por `Failed to generate SSPI context` dentro del sandbox. No es evidencia de una regresión M-602 y no se contabiliza como suite correcta.

Después de añadir únicamente documentación, evidencia y el verificador:

- análisis sintáctico PowerShell del verificador: correcto;
- `Verify-M602SecurityVerification.ps1 -SkipOnlineAnalysis -SkipRuntimeAnalysis`: correcto;
- `Verify-M602SecurityVerification.ps1 -EvidencePath mejoras/evidencias/M-602-security-verification-20260901.json`: correcto, incluidas consultas online y runtime;
- no se ejecutó ningún build, restore ni cambio de código de producto.

## 5. Decisiones

1. No se modifica código de producción: el análisis no confirmó una vulnerabilidad que justificara remediación.
2. El cierre combina controles de fuente, advisories online y una prueba runtime. Ninguna de esas categorías sustituye a las otras.
3. El binario runtime se identifica por SHA-256 en la evidencia; como M-602 solo añade artefactos de verificación, no existe una diferencia de código de producto que requiera recompilarlo.
4. La inspección del binding IIS instalado queda fuera del estado versionado. Si cambia el despliegue, debe repetirse `Manage-M306LocalHttps.ps1 -Action Verify`; la defensa de aplicación sigue rechazando binding y cliente no loopback.
5. No se inicia M-603 ni se corrigen hitos emergentes.

## 6. Conclusión

M-602 queda **APTO**: no se encontraron vulnerabilidades confirmadas en las superficies revisadas, NuGet y npm informaron cero advisories, la aplicación rechazó una escucha no loopback, el listener válido quedó limitado a `127.0.0.1`, y RSS, CSV y CSP conservaron sus controles. Las limitaciones del análisis y la evidencia negativa de la suite completa dentro del sandbox quedan registradas sin presentarlas como éxitos.
