# Carpeta de Mejoras y Calidad — La Primitiva Audit

Bienvenido al repositorio documental de evolución, calidad, seguridad y auditoría de **La Primitiva Audit Web App**.

Este directorio centraliza el plan director del proyecto, los informes de auditorías independientes por fase, las guías de arquitectura y operaciones, los análisis de verificación y las evidencias criptográficas y de ejecución.

---

## 🗺️ Mapa de Navegación Rápida

```
mejoras/
│
├── 📋 PLAN DIRECTOR
│   └── PLAN_DE_MEJORAS.md                      <- Plan de acción vivo, estado de hitos (M-000 a M-716)
│
├── 🛡️ INFORMES DE AUDITORÍA EXTERNA INDEPENDIENTE
│   ├── AUDITORIA_FASES_0_1_2.md                <- Fases 0 (Línea base), 1 (Integridad) y 2 (Funcional)
│   ├── AUDITORIA_FASE_3.md                     <- Fase 3: Seguridad Local Robusta
│   ├── AUDITORIA_FASE_4.md                     <- Fase 4: Persistencia y Arquitectura (Clean Arch & EF Core)
│   ├── AUDITORIA_FASE_5.md                     <- Fase 5: Calidad, Observabilidad y Mantenimiento (.NET 10 & MTP)
│   └── AUDITORIA_FASE_6.md                     <- Fase 6: Verificación Final (M-601, M-602, M-603)
│
├── 📘 GUÍAS Y PROCEDIMIENTOS OPERATIVOS
│   ├── LINEA_BASE_M000.md                      <- Flujos críticos y línea base verificable (M-000)
│   ├── RECUPERACION_BACKUPS.md                 <- Procedimiento de restauración y comprobación de backups (M-102)
│   ├── GUIA_M306_HTTPS_IIS.md                  <- Configuración HTTPS, PKI local y bindings IIS (M-306)
│   ├── ESTRATEGIA_PRUEBAS_M501.md              <- Pirámide y estrategia de pruebas automatizadas (M-501)
│   ├── TAXONOMIA_DE_ERRORES.md                 <- Catálogo semántico transversal de excepciones (M-506)
│   ├── LOCALIZACION.md                         <- Estrategia de globalización y cultura es-ES (M-507)
│   └── SIMULACRO_RECUPERACION_M603.md          <- Protocolo de ejecución del simulacro DRP (M-603)
│
├── 🔬 INFORMES DE VERIFICACIÓN Y ANÁLISIS
│   ├── VERIFICACION_FUNCIONAL_M601.md          <- Verificación funcional extremo a extremo (M-601)
│   ├── VERIFICACION_SEGURIDAD_M602.md          <- Verificación de seguridad multifrontera (M-602)
│   └── M-702_COMPARACION_ESTRATEGIAS.md        <- Evaluación empírica de estrategias estadísticas (M-702)
│
└── 📦 EVIDENCIAS
    └── evidencias/                             <- JSONs inmutables, scans, backtests y snapshots de BD
```

---

## 📚 Catálogo Detallado por Área

### 1. Plan Director
* [**`PLAN_DE_MEJORAS.md`**](PLAN_DE_MEJORAS.md): Documento vivo central. Registra el alcance, dependencias, criterios de aceptación, pruebas y estado de cada hito (desde el M-000 fundacional hasta los hitos emergentes de la Fase 7).

---

### 2. Informes de Auditoría Externa Independiente
Informes emitidos bajo criterios Senior de arquitectura, con dictámenes formales, matrices de trazabilidad y diagramas de flujo:
* [**`AUDITORIA_FASES_0_1_2.md`**](AUDITORIA_FASES_0_1_2.md): Evaluación de la línea base, aislamiento de pruebas de integración, resiliencia en backups con checksums y correcciones del modelo de dominio.
* [**`AUDITORIA_FASE_3.md`**](AUDITORIA_FASE_3.md): Evaluación del aislamiento de red loopback, autoalojamiento de assets (eliminación de CDN), CSP estricta, PKI local y neutralización de inyección CSV.
* [**`AUDITORIA_FASE_4.md`**](AUDITORIA_FASE_4.md): Evaluación de la adopción de `IDbContextFactory`, migraciones idempotentes EF Core, control de concurrencia optimista (`rowversion`) y Clean Architecture pura.
* [**`AUDITORIA_FASE_5.md`**](AUDITORIA_FASE_5.md): Evaluación del logging JSON estructurado y correlacionado, modernización a .NET 10 y xUnit v3 / MTP, taxonomía de errores y cultura global `es-ES`.
* [**`AUDITORIA_FASE_6.md`**](AUDITORIA_FASE_6.md): Evaluación de la verificación funcional (182/182 tests), scan de seguridad con 0 hallazgos y simulacro de recuperación en 6,5 segundos.

---

### 3. Guías y Procedimientos Operativos
Documentación técnica práctica y reproducible para el equipo de desarrollo y administración:
* [**`LINEA_BASE_M000.md`**](LINEA_BASE_M000.md): Definición de los 9 flujos críticos y arranque seguro de bases de datos.
* [**`RECUPERACION_BACKUPS.md`**](RECUPERACION_BACKUPS.md): Políticas de retención, ubicaciones y pruebas de restauración con `RESTORE VERIFYONLY` y `DBCC CHECKDB`.
* [**`GUIA_M306_HTTPS_IIS.md`**](GUIA_M306_HTTPS_IIS.md): Manual paso a paso para la generación de CA raíz local, certificados de servidor SAN `laprimitiva.local`, bindings IIS y configuración de navegadores (Firefox / Chrome).
* [**`ESTRATEGIA_PRUEBAS_M501.md`**](ESTRATEGIA_PRUEBAS_M501.md): Pirámide de pruebas (estáticas, rápidas en memoria e integración SQL con sufijo `_IntegrationTests`).
* [**`TAXONOMIA_DE_ERRORES.md`**](TAXONOMIA_DE_ERRORES.md): Jerarquía de excepciones de dominio, traductores de persistencia en infraestructura y límites correlacionados en Blazor (`AppErrorBoundary`).
* [**`LOCALIZACION.md`**](LOCALIZACION.md): Configuración de `es-ES`, segregación en 12 catálogos RESX por bounded context y preservación de neutralidad cultural en contratos (CSV, RSS, SQL).
* [**`SIMULACRO_RECUPERACION_M603.md`**](SIMULACRO_RECUPERACION_M603.md): Procedimiento automatizado para probar la recuperación física completa y arranque de la aplicación contra una réplica temporal.

---

### 4. Informes de Verificación y Análisis
Estudios técnicos y contrastes empíricos de algoritmos y características específicas:
* [**`VERIFICACION_FUNCIONAL_M601.md`**](VERIFICACION_FUNCIONAL_M601.md): Recorrido real por los 10 flujos críticos sobre base aislada y comprobación matemática canónica de importes (Coste `5,25 €`, Premios `38,00 €`, Neto `32,75 €`, ROI `623,8 %`).
* [**`VERIFICACION_SEGURIDAD_M602.md`**](VERIFICACION_SEGURIDAD_M602.md): Scan de seguridad Codex (6 superficies), auditoría de dependencias NuGet y npm (0 vulnerabilidades) y comprobación runtime de escucha y cabeceras.
* [**`M-702_COMPARACION_ESTRATEGIAS.md`**](M-702_COMPARACION_ESTRATEGIAS.md): Backtest walk-forward sobre 4.074 sorteos históricos y demostración de honestidad estadística de la selección uniforme frente a modelos predictivos o bayesianos.

---

### 5. Evidencias de Ejecución (`evidencias/`)
Ficheros JSON estructurados con marcas temporales, hashes SHA-256 y métricas inmutables generados por los scripts de verificación (`M-102-restore-*.json`, `M-602-security-verification-*.json`, `M-603-recovery-drill-*.json`, `M-702-strategy-comparison-*.json`).