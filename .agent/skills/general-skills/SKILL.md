---
name: General Development Skills
description: Core principles, architecture, and workflow standards for the project.
---

# Habilidades Generales y Estándares de Desarrollo

## 1. Filosofía de Arquitectura (Lean & Modular)
El proyecto sigue una arquitectura de **Monolito Modular** basada en **Slices Verticales**, estructurada en 3 capas estrictas pero organizadas por funcionalidad (Features).

### Capas del Sistema (Aligned with ABP.io DDD)
1. **Presentation Layer (API/UI)**: 
   - Endpoints (Minimal APIs), Controladores, ViewModels de Blazor.
   - Responsabilidad: I/O, Validación básica, Orquestación de UI.
   - **Prohibido**: Lógica de negocio compleja.
2. **Business Logic Layer (Domain/Application)**: 
   - Domain: Entidades, Value Objects, Reglas de negocio puras.
   - Application: Servicios de aplicación, DTOs, Orquestación de casos de uso via Managers.
   - **Tecnología**: .NET 10, HybridCache.
3. **Data Access Layer (Infrastructure)**: 
   - Implementaciones de Repositorios, EF Core 10, Integraciones externas.
   - **Optimización**: `ExecuteUpdateAsync`, `AsNoTracking`.

## 2. Metodología TDD (Test Driven Development)
**No se escribe código de producción sin un test que falle previamente.**

1. **🔴 RED**: Escribir un test unitario o de integración que falle.
2. **🟢 GREEN**: Escribir el código mínimo necesario para pasar el test.
3. **🔵 REFACTOR**: Mejorar el código (Clean Code, Performance) manteniendo el test en verde.

## 3. Principios de Código (Lean & Clean)
- **YAGNI (You Ain't Gonna Need It)**: No crear abstracciones anticipadas.
- **KISS (Keep It Simple, Stupid)**: La solución más simple suele ser la correcta.
- **DRY (Don't Repeat Yourself)**: Extraer lógica común con cuidado de no acoplar.
- **C# Moderno**: Usar las últimas características de C# (Records, Primary Constructors, Pattern Matching).

## 4. Estándares de Documentación
**Todo el código público y lógico compleja debe estar documentado en ESPAÑOL DE ESPAÑA.**

- **XML Comments**: Usar `///` para documentar clases, métodos y propiedades.
- **Idioma**: Español (España). Evitar anglicismos innecesarios en la documentación, pero mantenerlos en el código (naming conventions en inglés).
- **Legibilidad**: Comentarios claros que expliquen el *POR QUÉ*, no el *QUÉ* (el código ya dice el qué).

```csharp
/// <summary>
/// Calcula el total del pedido aplicando los descuentos de temporada.
/// </summary>
/// <param name="orderId">Identificador único del pedido.</param>
/// <returns>El importe total calculado.</returns>
/// <exception cref="OrderNotFoundException">Se lanza si el pedido no existe.</exception>
public async Task<decimal> CalculateTotalAsync(Guid orderId) { ... }
```

## 5. Gestión de Constantes y Literales
**Prohibido el uso de "Magic Strings" o números mágicos en el código.**

- **Constantes**: Agrupar valores fijos en clases estáticas `Constants` o `Enums` específicos.
- **Configuración**: Valores que pueden cambiar (URLs, Timeouts) deben ir en `appsettings.json`.
- **Enums**: Usar `Enums` para estados, tipos y opciones limitadas.

## 6. Internacionalización y Textos (Localization)
**Está prohibido hardcodear textos visibles para el usuario en el código.**

- **Recursos**: Todos los mensajes de error, etiquetas de UI y notificaciones deben estar en archivos de recursos (`.resx`) o un proveedor de localización centralizado.
- **Idioma Base**: Español (España).
- **Separación**: Mantener los textos en una capa o servicio de localización dedicado, permitiendo futura traducción sin recompilar lógica.

## 7. Seguridad y Ciberseguridad (Security First)
**La seguridad es responsabilidad de todos, no una fase final.**

- **OWASP Top 10**: Tener siempre presente los riesgos más comunes (Inyección, Autenticación Rota, Exposición de Datos).
- **Principio de Mínimo Privilegio**: Servicios y bases de datos deben correr con los permisos mínimos necesarios.
- **Validación de Entrada**: Nunca confiar en el input del usuario. Validar en Frontend (UX) y Backend (Seguridad).
- **Datos Sensibles**: No loggear datos personales (PII), contraseñas o tokens.
- **Defensa en Profundidad**: Aplicar capas de seguridad (WAF, Validación, Autenticación, Encriptación).
