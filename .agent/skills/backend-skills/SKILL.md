---
name: Backend Development Skills
description: Backend guidelines focusing on ABP.io patterns, .NET 10, EF Core, and IIS hosting.
---

# Habilidades Backend (ABP.io & .NET 10)

## 1. Patrones ABP.io & Domain Driven Design (DDD)
La arquitectura backend sigue estrictamente los patrones de [ABP.io](https://abp.io/docs/latest/).

- **Domain Layer**: 
  - Entidades, Value Objects, Aggregates.
  - Define las interfaces de repositorio (abstractas).
  - Managers de dominio para lógica compleja entre entidades.
- **Application Layer**: 
  - Implementa casos de uso mediante `ApplicationServices`.
  - Mapeo de Entidades a DTOs (nunca devolver entidades).
  - Validación de entrada.
- **Infrastructure Layer**: 
  - Implementación de Repositorios (EF Core).
  - Integraciones externas (Email, SMS, Pasarelas de pago).

## 2. Tecnologías Core (.NET 10)
- **Minimal APIs vs Controllers**: Preferir Minimal APIs para endpoints ligeros y de alto rendimiento.
- **Primary Constructors**: Obligatorio en Servicios, Clases y DTOs para reducir boilerplate.
  ```csharp
  // Ejemplo Primary Constructor
  public class ProductAppService(IProductRepository repository, ILogger<ProductAppService> logger) : ApplicationService
  {
      private readonly IProductRepository _repository = repository; // Opcional si se usa field
      public async Task CreateAsync(CreateProductDto input) { ... }
  }
  ```
- **Field Keyword**: Utilizar `field` para propiedades con lógica en el setter.

## 3. Data Access (EF Core 10 & SQL Server)
- **Base de Datos**: SQL Server.
- **Performance**:
  - `ExecuteUpdateAsync` / `ExecuteDeleteAsync` para operaciones en lote.
  - `AsNoTracking()` para todas las consultas de lectura (`IReadOnlyRepository`).
  - **HybridCache**: Implementar caché híbrido para datos maestros o de configuración.

## 4. Despliegue y Hosting (Windows IIS)
El entorno de producción es **Windows Server con IIS**.

- **Configuración (`web.config`)**: Asegurar que la configuración de integración de ASP.NET Core está optimizada.
- **Loggs**: Configurar Serilog/OpenTelemetry para escribir en sumideros compatibles con el entorno (File/EventLog si no hay collector centralizado).
- **Service Lifetime**: Gestionar correctamente el ciclo de vida de los servicios (Scoped por request, Singleton para caché/config).
- **AppPool**: Configurar como `No Managed Code` ya que Kestrel maneja el proceso, IIS actúa como proxy reverso.

## 5. Testing de Integración
- Usar `WebApplicationFactory` para levantar el contexto completo in-memory o con TestContainers (SQL Server).
- Validar flujos completos desde el Application Service hasta la Base de Datos.

## 6. Gestión de Excepciones y Recursos
**La gestión de errores debe ser centralizada y localizada.**

- **Exception Middleware**: Utilizar el middleware de excepciones de ABP o uno personalizado para capturar errores no controlados. **No usar try-catch en lógica de negocio** salvo para recuperación específica.
- **Business Exceptions**: Lanzar excepciones de negocio tipadas (`BusinessException`) con códigos de error específicos (`DomainErrors.UserNotFound`).
- **Resource Files**: Los mensajes de error deben provenir de archivos de recursos (`.resx`) compartidos (Domain.Shared) para facilitar la traducción.

## 7. Seguridad Backend (Defensa en Profundidad)
**El backend no debe confiar nunca en el cliente.**

- **Inyección SQL**: Uso obligatorio de EF Core con consultas parametrizadas (LINQ). Prohibido concatenar strings en `FromSqlRaw`.
- **Validación de Datos**: Usar `FluentValidation` o DataAnnotations para validar todos los DTOs de entrada. Rechazar datos malformados (`400 Bad Request`).
- **Autenticación y Autorización**: 
  - Usar el sistema de permisos de ABP (`[Authorize(Permissions.MyPermission)]`).
  - No implementar esquemas de autenticación propios; usar estándares (OpenID Connect, JWT).
- **Protección de Datos**: Encriptar datos sensibles en reposo y en tránsito (HTTPS obligatorio).
