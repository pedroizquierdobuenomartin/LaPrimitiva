# La Primitiva Audit - Web App

App multipágina para registrar y auditar juegos de La Primitiva (España), comparando combinaciones fijas vs automáticas con soporte multi-año.

## 🚀 Requisitos y Configuración Local

1. **Stack**: .NET 10, Blazor Server, EF Core, SQL Server LocalDB.
2. **Base de Datos**: Usa LocalDB de Windows. El seeding es automático en el primer arranque.
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
