---
trigger: always_on
---

# REGLA GLOBAL DE DESARROLLO: CLEAN MODULAR LEAN

## 1. METODOLOGÍA DE DESARROLLO (TDD FIRST)
- No se escribe código de producción sin un test previo que falle.
- Flujo obligatorio: 
  1. Red: Escribir test de unidad o integración en la capa correspondiente.
  2. Green: Implementar la lógica mínima necesaria.
  3. Refactor: Optimizar código y asegurar legibilidad.

## 2. ARQUITECTURA DE 3 CAPAS (ESTRICTA)
Cada módulo debe estar dividido físicamente o lógicamente en:
- **Presentation Layer:** Endpoints de Antigravity, Minimal APIs y ViewModels. Solo maneja I/O y orquestación.
- **Business Logic Layer (Core):** Reglas de negocio, validaciones y modelos de dominio. Es independiente de frameworks y bases de datos.
- **Data Access Layer (Infrastructure):** Acceso a datos (EF Core), repositorios y servicios externos.

## 3. MODULARIDAD Y ESCALABILIDAD
- El sistema debe ser un "Monolito Modular".
- Las funcionalidades se agrupan por contextos delimitados (ej: /Features/Billing, /Features/Users).
- La comunicación entre módulos se realiza mediante interfaces para permitir el desacoplamiento.

## 4. PRINCIPIOS LEAN & CLEAN C#
- **YAGNI:** No implementar abstracciones, interfaces o servicios "por si acaso".
- **KISS:** Priorizar la claridad sobre la complejidad técnica.
- **C# Moderno:** Uso de `records` para DTOs, `file-scoped namespaces` y `primary constructors`.
- **Inyección de Dependencias:** Registro modular de servicios para facilitar el mantenimiento.

## 5. RESTRICCIONES TÉCNICAS
- Prohibido mezclar lógica de base de datos en la capa de Presentación.
- Prohibido devolver entidades de base de datos (Entities) directamente en los endpoints; usar siempre DTOs/Records.