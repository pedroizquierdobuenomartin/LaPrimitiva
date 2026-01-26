---
name: Frontend Development Skills
description: Frontend guidelines focusing on Blazor, Tailwind CSS, and ABP.io architecture.
---

# Habilidades Frontend (Blazor & Tailwind CSS)

## 1. Arquitectura de UI (ABP.io & Blazor)
El frontend se construye sobre **Blazor** siguiendo los principios de modularidad de ABP.io.

- **Componentes**: Deben ser pequeños, reutilizables y atómicos.
- **Razor Pages**: Usar `.razor` para la definición de la UI y `.razor.cs` (Code-Behind) para la lógica de presentación si el componente es complejo.
- **ViewModels**: Utilizar ViewModels para desacoplar la UI de los DTOs del backend cuando sea necesario formateo o estado de vista complejo.

## 2. Estilo y Diseño (Tailwind CSS)
El sistema de diseño se basa exclusivamente en **Tailwind CSS**.

- **Utility-First**: Usar clases de utilidad directamente en el HTML.
- **Configuración**: Centralizar colores y temas en `tailwind.config.js`.
- **Aesthetic**: Diseño moderno, premium, con micro-interacciones (hover, focus states).
- **Prohibido**: CSS inline (`style="..."`) o archivos `.css` aislados, salvo para componentes muy específicos que lo requieran (Scoped CSS).

## 3. Gestión de Estado y Comunicación
- **Application Services**: El frontend consume los `ApplicationServices` a través de proxies generados por ABP o clientes HTTP tipados.
- **DTOs**: Sólo se intercambian DTOs (Data Transfer Objects) con el backend. **NUNCA** referencias a Entidades de dominio.
- **Estado Global**: Usar servicios inyectados (`Scoped`/`Singleton`) para mantener estado entre páginas si es necesario, evitando `static`.

## 4. Documentación
Seguir las mismas reglas de documentación que el backend:
- **Idioma**: Español (España).
- **XML Comments**: Documentar componentes públicos (`Parameters`) y métodos complejos.

## 5. Textos y Localización
**Prohibido textos hardcodeados en los componentes Razor.**

- **IStringLocalizer**: Usar `IStringLocalizer<Resource>` para todos los textos visibles.
- **Resources**: Usar los mismos archivos de recursos compartidos del backend (Domain.Shared) para mantener consistencia.
- **Atributos de Validación**: Usar `ErrorMessageResourceName` y `ErrorMessageResourceType` en los DataAnnotations de los ViewModels.

## 6. Seguridad Frontend (Ciberseguridad en el Cliente)
**Protección activa contra ataques en el navegador.**

- **XSS (Cross-Site Scripting)**: Blazor sanitiza por defecto. **Prohibido usar `MarkupString`** con contenido generado por usuarios sin sanitización previa.
- **CSRF (Cross-Site Request Forgery)**: Asegurar que los Anti-Forgery Tokens de ABP se envían en todas las peticiones POST/PUT/DELETE.
- **Almacenamiento Local**: **Nunca almacenar tokens de acceso, contraseñas o PII en `localStorage` o `sessionStorage`**. Usar Cookies `HttpOnly; Secure`.
- **Content Security Policy (CSP)**: Configurar cabeceras CSP estrictas para evitar carga de scripts no autorizados.
