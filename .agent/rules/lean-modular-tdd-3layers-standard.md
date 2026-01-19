---
trigger: always_on
---

# REGLA GLOBAL: VANGUARDIA .NET 10 & LEAN ARCHITECTURE

## 1. DESARROLLO GUIADO POR PRUEBAS (TDD & OBSERVARBILIDAD)
- No se escribe código de producción sin un test previo que falle.
- **Ciclo TDD Estricto:** Rojo (Test falla) -> Verde (Código mínimo) -> Refactor.
- Flujo obligatorio: 
  1. Red: Escribir test de unidad o integración en la capa correspondiente.
  2. Green: Implementar la lógica mínima necesaria.
  3. Refactor: Optimizar código y asegurar legibilidad.
- **Integration Testing:** Usar `WebApplicationFactory` y `TestContainers` para validar la integración real entre capas sin mocks excesivos.
- **Métricas:** Incorporar `System.Diagnostics.Metrics` desde el inicio para telemetría nativa.

## 2. ARQUITECTURA MODULAR DE 3 CAPAS (Slices) (ESTRICTA)
- **Presentation Layer:** Endpoints de Antigravity, Minimal APIs y ViewModels. Uso obligatorio de `MapStaticAssets` para optimización de recursos. Solo maneja I/O y orquestación.
- **Business Logic Layer (Core - Negocio):** Lógica pura. Uso de `HybridCache` para gestión de estado de alto rendimiento (evita el cache stampede). Reglas de negocio, validaciones y modelos de dominio. Es independiente de frameworks y bases de datos.
- **Data Access Layer (Infrastructure):** EF Core 10. Priorizar `ExecuteUpdateAsync` y `ExecuteDeleteAsync` para operaciones masivas y `AsNoTracking()` en lecturas. Acceso a datos (EF Core), repositorios y servicios externos.

## 3. MODULARIDAD Y ESCALABILIDAD
- El sistema debe ser un "Monolito Modular".
- Las funcionalidades se agrupan por contextos delimitados (ej: /Features/Billing, /Features/Users).
- La comunicación entre módulos se realiza mediante interfaces para permitir el desacoplamiento.

## 4. C# 14 LEAN SYNTAX (Eliminación de Boilerplate)
- **Field Keyword:** Usar el keyword `field` en propiedades para evitar declarar variables privadas manuales.
- **Extension Members:** Organizar utilidades en bloques `extension` para mantener el Core limpio.
- **Primary Constructors:** Obligatorios en servicios y DTOs para inyección de dependencias concisa.
- **Null-Conditional Assignment:** Usar `obj?.Property = value` para simplificar flujos de validación.

## 5. PRINCIPIOS LEAN & CLEAN C#
- **YAGNI:** No implementar abstracciones, interfaces o servicios "por si acaso".
- **KISS:** Priorizar la claridad sobre la complejidad técnica.
- **C# Moderno:** Uso de `records` para DTOs, `file-scoped namespaces` y `primary constructors`.
- **Inyección de Dependencias:** Registro modular de servicios para facilitar el mantenimiento.

## 6. ESCALABILIDAD Y RENDIMIENTO (Native AOT Ready)
- **AOT Compatibility:** El código debe ser compatible con Native AOT (evitar reflexión pesada) para permitir despliegues ultraligeros y arranque instantáneo.
- **Modular Monolith:** Comunicación entre módulos exclusivamente mediante interfaces o eventos in-memory (MediatR). Prohibido que un módulo acceda a la DB de otro.

## 7. RESTRICCIONES TÉCNICAS ADICIONALES
- **Records:** Uso de `public record` para cada DTO.
- **Result Pattern:** Prohibido lanzar excepciones para lógica de negocio; usar un objeto `Result` o `OneOf`.
- **YAGNI:** No abstraer interfaces si solo existe una implementación, a menos que sea necesario para el test.
- Prohibido mezclar lógica de base de datos en la capa de Presentación.
- Prohibido devolver entidades de base de datos (Entities) directamente en los endpoints; usar siempre DTOs/Records.