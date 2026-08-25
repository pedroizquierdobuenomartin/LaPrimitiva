# La Primitiva Audit - Web App

App multipágina para registrar y auditar juegos de La Primitiva (España), comparando combinaciones fijas vs automáticas con soporte multi-año.

## 🚀 Requisitos y Configuración Local

1. **Stack**: .NET 10, Blazor Server, EF Core y SQL Server Express.
2. **Base de datos**: la configuración local apunta a la instancia y base definidas en `LaPrimitiva.App/appsettings.json`. El esquema se administra exclusivamente mediante migraciones EF Core; el seeding de datos históricos se ejecuta al arrancar, una vez migrada la base.
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

### Migraciones de base de datos

La aplicación **no crea ni modifica tablas al arrancar**. Antes del primer arranque y antes de desplegar una versión con migraciones nuevas, una identidad administrativa separada debe aplicar las migraciones:

```powershell
.\scripts\Invoke-M401DatabaseMigration.ps1 -Action Update
```

La conexión puede indicarse con `-ConnectionString` o mediante `LAPRIMITIVA_MIGRATION_CONNECTION`; si no se proporciona, el script usa `ConnectionStrings:DefaultConnection` de `LaPrimitiva.App/appsettings.json`. Conviene realizar antes el backup verificado descrito en M-101. Las migraciones iniciales reconocen el esquema legado que el seeder creaba sin historial, registran la cadena de migraciones y conservan sus filas.

Para entregar el cambio a un administrador de base de datos sin conceder DDL a la identidad de ejecución de la aplicación, generar un script idempotente derivado de EF Core:

```powershell
.\scripts\Invoke-M401DatabaseMigration.ps1 -Action Script
```

El fichero queda en `artifacts\database\LaPrimitiva.Migrations.sql` (fuera de Git). Tras aplicarlo, la identidad normal de la aplicación solo necesita los permisos de lectura y escritura requeridos por sus casos de uso; no necesita permisos permanentes para crear o alterar tablas. `-NoBuild` solo debe usarse cuando los binarios ya se hayan compilado con exactamente la versión que se va a migrar.

### Seguridad de acceso exclusivamente local

La aplicación está diseñada para ejecutarse sin autenticación **solo en el equipo local**:

- Al arrancar, rechaza cualquier `urls`, `ASPNETCORE_URLS` o endpoint Kestrel que no use `localhost`, `127.0.0.1` o `::1`. Las configuraciones abreviadas `HTTP_PORTS` y `HTTPS_PORTS` también se rechazan porque publican mediante comodín.
- Durante cada petición, rechaza con `403` cualquier dirección remota que no sea loopback.
- El filtrado de host solo admite `laprimitiva.local`, `localhost`, `127.0.0.1` y `[::1]`; cualquier otro host recibe `400`.

Para publicar en IIS se usa exclusivamente `https://laprimitiva.local/`:

La guía operativa completa, con instalación en el primer equipo, traslado a otros ordenadores, renovación, retirada y diagnóstico, está en [`mejoras/GUIA_M306_HTTPS_IIS.md`](mejoras/GUIA_M306_HTTPS_IIS.md).

1. Añadir `127.0.0.1 laprimitiva.local` al archivo local `C:\Windows\System32\drivers\etc\hosts`.
2. Publicar la aplicación y crear el sitio IIS sin un binding accesible desde la LAN. El pool debe usar `No Managed Code`.
3. Abrir **Windows PowerShell 5.1 como administrador** desde la raíz del repositorio. PowerShell 7 no carga de forma fiable el proveedor `WebAdministration` utilizado por el script.
4. Crear la CA de desarrollo, confiar en ella, emitir el certificado con SAN y configurar el binding `127.0.0.1:443:laprimitiva.local` con SNI:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Manage-M306LocalHttps.ps1 `
     -Action Create -SiteName 'laprimitiva.local'
   ```
   Sustituir `laprimitiva.local` por el nombre exacto devuelto por `Get-Website` si el sitio se llama de otra forma. El script solicita la contraseña de exportación de la PFX, configura HTTPS y retira cualquier binding HTTP del mismo sitio para `laprimitiva.local:80`; no modifica bindings de otros hosts.
5. Iniciar el sitio y realizar la comprobación operativa:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Manage-M306LocalHttps.ps1 `
     -Action Verify -SiteName 'laprimitiva.local'
   ```
   La verificación exige confianza de cadena, vigencia, SAN `laprimitiva.local`, SNI, resolución a `127.0.0.1`, ausencia del binding HTTP, respuesta `200` sin omitir errores TLS y cabecera HSTS.

   Firefox debe confiar en la CA de Windows o tener importada `LaPrimitiva-Local-Root-CA.cer` como autoridad. Aceptar una excepción para continuar y conservar el indicador **No seguro** no satisface la validación; la guía contiene el procedimiento específico para Firefox.

### Certificado instalable en otros ordenadores locales

`-Action Create` exporta un paquete instalable en `artifacts\local-https`:

- `LaPrimitiva-Local-Root-CA.cer`: certificado público de la CA; puede distribuirse para establecer la confianza.
- `laprimitiva.local.cer`: certificado público del servidor para inspección.
- `laprimitiva.local.pfx`: paquete secreto que contiene el certificado público del servidor y su clave privada, protegido con la contraseña solicitada; permite configurar otro IIS local con el mismo nombre.

En el equipo que ejecuta `Create` no hay que importar manualmente ninguno de estos ficheros: el script ya instala los certificados y configura IIS. La tabla de la guía explica qué contiene cada formato y, para otro equipo, qué almacén corresponde a cada uno.

La CA privada no se exporta. La PFX debe transferirse por un canal seguro y su contraseña por otro canal. **No se versiona** ningún PFX, contraseña ni clave privada; `artifacts\` y las extensiones de clave están ignoradas por Git. Para instalar el paquete descargado en otro equipo, copiar los dos ficheros necesarios fuera del repositorio y ejecutar:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Manage-M306LocalHttps.ps1 `
  -Action Install -SiteName 'laprimitiva.local' `
  -RootCertificatePath 'C:\Descargas\LaPrimitiva-Local-Root-CA.cer' `
  -PfxPath 'C:\Descargas\laprimitiva.local.pfx'
```

Cada equipo debe conservar la entrada de `hosts` limitada a `127.0.0.1`. Para renovar, ejecutar de nuevo `-Action Create`: mientras siga vigente, se reutiliza la misma CA privada local, se emite un certificado de servidor nuevo, se cambia el binding, se retiran los certificados de servidor anteriores y se vuelve a exportar el paquete. Para retirar el servidor y su binding:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Manage-M306LocalHttps.ps1 `
  -Action Remove -SiteName 'laprimitiva.local' -RemoveRoot
```

No usar `Todos sin asignar`, una IP LAN ni un binding comodín. La aplicación rechazará clientes no locales aunque IIS quede configurado de forma más amplia, pero el binding de IIS también debe limitarse a loopback como primera barrera.

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
