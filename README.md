# La Primitiva Audit - Web App

App multipágina para registrar y auditar juegos de La Primitiva (España), comparando combinaciones fijas vs automáticas con soporte multi-año.

## 🚀 Requisitos y Configuración Local

1. **Stack**: .NET 10, Blazor Server, EF Core y SQL Server Express.
2. **Base de datos**: la configuración de desarrollo apunta a la instancia `localhost\\SQLEXPRESS` y a la base `PrimitivaAuditV2`. El seeding es automático en el primer arranque.
3. **Ejecución**:
   - **Opción A (VS Code)**: Presiona `F5` y selecciona el perfil `.NET Core Launch (Web)`.
   - **Opción B (Terminal desde la raíz)**:
     ```bash
     dotnet run --project LaPrimitiva.App
     ```
   - **Opción C (Terminal desde carpeta)**:
     ```bash
     cd LaPrimitiva.App
     dotnet run
     ```
   La aplicación estará disponible en `http://localhost:5007`.

### Seguridad de acceso exclusivamente local

La aplicación está diseñada para ejecutarse sin autenticación **solo en el equipo local**:

- Al arrancar, rechaza cualquier `urls`, `ASPNETCORE_URLS` o endpoint Kestrel que no use `localhost`, `127.0.0.1` o `::1`. Las configuraciones abreviadas `HTTP_PORTS` y `HTTPS_PORTS` también se rechazan porque publican mediante comodín.
- Durante cada petición, rechaza con `403` cualquier dirección remota que no sea loopback.
- El filtrado de host solo admite `laprimitiva.local`, `localhost`, `127.0.0.1` y `[::1]`; cualquier otro host recibe `400`.

Para publicar en IIS como `http://laprimitiva.local/`:

1. Añadir `127.0.0.1 laprimitiva.local` al archivo local `C:\Windows\System32\drivers\etc\hosts`.
2. Configurar el binding HTTP del sitio con IP `127.0.0.1`, puerto `80` y nombre de host `laprimitiva.local`.
3. No usar `Todos sin asignar`, una IP LAN ni un binding comodín. La aplicación rechazará clientes no locales aunque IIS quede configurado de forma más amplia, pero el binding de IIS también debe limitarse a loopback como primera barrera.

Esta política local no sustituye autenticación ni autorización. Si en el futuro se habilita acceso LAN, debe implementarse ese modelo de seguridad antes de retirar estas restricciones.

## 🛠️ Funcionalidades Implementadas

- **Dashboard**: Vista clara de KPIs (Gasto, Ganado, Neto, ROI) y desglose por tipo de apuesta.
- **Registro**: Tabla interactiva para marcar sorteos jugados e introducir premios. Los cambios se guardan automáticamente.
- **Planes**: Sistema de versiones para cambiar costes (p.ej. subida de precio en futuros años) o activar/desactivar Joker sin romper el histórico anterior.
- **Datos**: Exportación completa a CSV.
- **Responsive**: Diseño premium con TailwindCSS, adaptable a móviles con sidebar lateral.

## 📁 Estructura del Proyecto

- `LaPrimitiva.Domain`: Entidades (`Plan`, `DrawRecord`) y lógica de negocio.
- `LaPrimitiva.Application`: Servicios de generación de calendario, cálculos y resúmenes.
- `LaPrimitiva.Infrastructure`: Persistencia SQL Server y configuraciones de EF Core.
- `LaPrimitiva.App`: Interfaz Blazor con componentes modernos y TailwindCSS.
- `LaPrimitiva.Tests`: Pruebas unitarias xUnit para validación de cálculos.

## ✅ Verificación de Cálculos
He incluido tests unitarios que validan:
- Cálculo de coste total incluyendo Joker.
- Cálculo de beneficio neto.
- Independencia de costes entre diferentes planes.

Ejecutar tests:
```bash
dotnet test
```

La preparación de SQL Server, la base exclusiva de integración, la matriz de flujos críticos y el resultado inicial verificable están documentados en [`mejoras/LINEA_BASE_M000.md`](mejoras/LINEA_BASE_M000.md).
