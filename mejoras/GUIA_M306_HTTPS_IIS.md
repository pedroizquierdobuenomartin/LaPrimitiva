# Guía M-306 — Instalar HTTPS local en IIS

Esta guía configura `https://laprimitiva.local/` exclusivamente en el propio equipo mediante un certificado de confianza y un binding IIS limitado a `127.0.0.1:443`.

> **Importante:** utiliza **Windows PowerShell 5.1 como administrador**. No uses PowerShell 7 (`pwsh`), porque el proveedor `WebAdministration` de IIS no funciona de forma fiable en ese entorno.

## 1. Qué vas a conseguir

- `laprimitiva.local` resolverá a `127.0.0.1`.
- IIS servirá la aplicación mediante HTTPS, con SNI y sin exposición LAN.
- El certificado contendrá `laprimitiva.local` en el SAN y tendrá una cadena de confianza válida.
- El binding HTTP de `laprimitiva.local:80` se retirará.
- Se generará un paquete instalable en otros equipos locales:
  - `LaPrimitiva-Local-Root-CA.cer`: CA pública de confianza.
  - `laprimitiva.local.cer`: certificado público del servidor.
  - `laprimitiva.local.pfx`: paquete con el certificado público del servidor y su clave privada protegida con contraseña.

Los ficheros se generan en `artifacts\local-https`, una ruta excluida de Git.

## 2. Requisitos previos

Comprueba antes de continuar:

1. IIS está instalado y dispone del módulo de administración de scripts.
2. El Hosting Bundle de ASP.NET Core correspondiente a la aplicación está instalado.
3. La aplicación ya está publicada en una carpeta local y existe un sitio IIS que apunta a ella.
4. El pool de aplicaciones usa **No Managed Code**.
5. Tienes permisos de administrador en Windows.
6. El sitio no define `ASPNETCORE_ENVIRONMENT=Development`; debe usar `Production` —valor predeterminado— para que la aplicación emita HSTS.

Si todavía necesitas generar la publicación, ejecuta `Publish.bat` manualmente desde la raíz. Esta operación compila la aplicación.

## 3. Abrir la consola correcta

1. Abre el menú Inicio.
2. Busca **Windows PowerShell**.
3. Pulsa **Ejecutar como administrador** y acepta el aviso UAC.
4. Sitúate en la raíz del repositorio:

```powershell
Set-Location 'E:\Repositorios\LaPrimitiva'
```

Confirma que estás usando Windows PowerShell 5.1 y que la consola está elevada:

```powershell
$PSVersionTable.PSEdition
([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)
```

Los resultados esperados son `Desktop` y `True`.

## 4. Identificar el sitio IIS

Carga IIS y muestra los sitios disponibles:

```powershell
Import-Module WebAdministration
Get-Website | Select-Object Name, State, PhysicalPath
```

Anota el valor exacto de `Name`. En esta instalación el sitio se llama `laprimitiva.local`; sustitúyelo si en otro equipo tiene un nombre diferente.

Comprueba que la ruta física es la publicación correcta:

```powershell
Get-ItemProperty 'IIS:\Sites\laprimitiva.local' | Select-Object Name, PhysicalPath, State
```

## 5. Configurar el nombre local

Abre el archivo de hosts con privilegios administrativos:

```powershell
notepad.exe "$env:windir\System32\drivers\etc\hosts"
```

Añade una única línea y guarda:

```text
127.0.0.1 laprimitiva.local
```

Limpia la caché DNS y verifica la resolución:

```powershell
ipconfig /flushdns
Resolve-DnsName laprimitiva.local -Type A
```

La dirección devuelta debe ser `127.0.0.1`. No uses una dirección LAN ni `0.0.0.0`.

## 6. Crear e instalar el certificado en el primer equipo

Ejecuta:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
   -File '.\scripts\Manage-M306LocalHttps.ps1' `
   -Action Create `
   -SiteName 'laprimitiva.local'
```

El script solicitará una contraseña para proteger la PFX. Usa una contraseña robusta y guárdala en un gestor de contraseñas; no la escribas en archivos del repositorio.

La acción realiza este proceso:

1. Crea o reutiliza la CA privada local de LaPrimitiva.
2. Instala su certificado público en `LocalMachine\Root`.
3. Emite un certificado de servidor con SAN `laprimitiva.local`.
4. Instala el certificado con clave privada en `LocalMachine\My`.
5. Crea el binding HTTPS `127.0.0.1:443:laprimitiva.local` con SNI.
6. Retira los bindings HTTP del mismo sitio para `laprimitiva.local:80`.
7. Exporta el paquete de distribución en `artifacts\local-https`.

### Qué contiene cada fichero

| Fichero | ¿Contiene clave privada? | Sensibilidad | Uso correcto |
|---|---:|---|---|
| `LaPrimitiva-Local-Root-CA.cer` | No | Público | Permite que Windows confíe en los certificados emitidos por la CA local. En otro equipo se instala en **Equipo local → Entidades de certificación raíz de confianza**. |
| `laprimitiva.local.cer` | No | Público | Copia pública del certificado del servidor para inspeccionar titular, SAN, vigencia y huella. No sirve por sí solo para configurar HTTPS en IIS y normalmente no se instala. |
| `laprimitiva.local.pfx` | **Sí** | **Secreto** | Contiene el certificado público del servidor **más su clave privada**. IIS necesita este paquete en otro equipo. Se instala en **Equipo local → Personal** y exige la contraseña elegida. |

La PFX no es «el certificado privado»: es un contenedor que reúne el certificado público y la clave privada. Los `.cer` contienen únicamente información pública.

La clave privada de la CA raíz permanece en `LocalMachine\My` del equipo que ejecutó `Create` y nunca se exporta a un fichero. Esto permite renovar certificados sin distribuir la clave capaz de emitirlos.

### ¿Debo usar el asistente de importación de Windows?

**En el primer equipo: no importes nada manualmente.** Si `Create` termina mostrando `HTTPS configurado para 127.0.0.1:443:laprimitiva.local`, el script ya ha instalado la CA, el certificado con clave privada y el binding IIS. Si has abierto el asistente haciendo doble clic en la PFX, pulsa **Cancelar**.

**En otro ordenador:** utiliza preferentemente `-Action Install`, explicado en el apartado 9. El script coloca cada certificado en su almacén correcto y configura IIS. No hace falta abrir ninguno de los tres ficheros con doble clic.

Si excepcionalmente realizas la importación manual:

1. Importa `LaPrimitiva-Local-Root-CA.cer` para **Equipo local** en **Entidades de certificación raíz de confianza**.
2. Importa `laprimitiva.local.pfx` para **Equipo local** en **Personal**, introduce la contraseña y no marques la clave como exportable salvo que exista una necesidad operativa justificada.
3. No importes `laprimitiva.local.cer`: es únicamente la copia pública de inspección.
4. La importación manual no crea el binding IIS; todavía tendrás que configurarlo. Por eso se recomienda `-Action Install`.

## 7. Revisar el binding y los certificados

Comprueba el binding:

```powershell
Get-WebBinding -Name 'laprimitiva.local' |
  Select-Object Protocol, BindingInformation, SslFlags, CertificateHash, CertificateStoreName
```

Debe existir un binding equivalente a:

```text
https  127.0.0.1:443:laprimitiva.local  SslFlags=1
```

No debe permanecer ningún binding HTTP para `laprimitiva.local`.

Comprueba los certificados:

```powershell
Get-ChildItem Cert:\LocalMachine\My |
  Where-Object Subject -eq 'CN=laprimitiva.local' |
  Select-Object Subject, Thumbprint, NotBefore, NotAfter, HasPrivateKey, DnsNameList

Get-ChildItem Cert:\LocalMachine\Root |
  Where-Object Subject -eq 'CN=LaPrimitiva Local Development Root CA' |
  Select-Object Subject, Thumbprint, NotBefore, NotAfter
```

## 8. Iniciar y verificar el sitio

Inicia el sitio si fuese necesario:

```powershell
Start-Website -Name 'laprimitiva.local'
```

Ejecuta la verificación automatizada:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\scripts\Manage-M306LocalHttps.ps1' `
  -Action Verify `
  -SiteName 'laprimitiva.local'
```

La salida correcta termina con:

```text
M-306 operativo: HTTPS confiable, SAN correcto, SNI activo, loopback y HSTS verificados para laprimitiva.local.
```

Finalmente, abre `https://laprimitiva.local/` en el navegador y comprueba:

1. No aparece ninguna advertencia de certificado.
2. El candado muestra un certificado vigente para `laprimitiva.local`.
3. La aplicación carga y funciona normalmente.
4. `http://laprimitiva.local/` no responde, porque el binding HTTP se ha retirado de forma explícita.

### Firefox: no aceptes una excepción permanente como validación

Si Firefox muestra una advertencia y ofrece **Aceptar el riesgo y continuar**, esa excepción solo permite saltarse el error para ese sitio. Mientras la barra indique **No seguro**, M-306 todavía no está validado: el navegador no está confiando correctamente en la CA.

Firefox 120 o posterior puede utilizar automáticamente las CA de terceros instaladas en el almacén de Windows. Mozilla documenta esta integración en [Automatically trust third-party root certificates](https://support.mozilla.org/en-US/kb/automatically-trust-third-party-certificates) y [Set up Certificate Authorities in Firefox](https://support.mozilla.org/en-US/kb/setting-certificate-authorities-firefox).

Para corregirlo:

1. En Firefox abre **Ajustes → Privacidad y seguridad**.
2. En la sección de seguridad/conexión y certificados, activa **Permitir que Firefox confíe automáticamente en los certificados raíz de terceros que instalas**.
3. Si esa opción no aparece, abre `about:config`, busca `security.enterprise_roots.enabled` y establece su valor en `true`.
4. Cierra **todas** las ventanas de Firefox y vuelve a abrirlo. Mozilla indica que el reinicio es necesario para volver a inspeccionar las CA de `LocalMachine`.
5. Elimina la excepción temporal anterior: abre **Ajustes → Privacidad y seguridad → Certificados → Ver certificados → Servidores**, selecciona la entrada de `laprimitiva.local` si existe y elimínala.
6. Vuelve a abrir exactamente `https://laprimitiva.local/`.
7. Comprueba que ya no aparece la página de advertencia y que la barra no indica **No seguro**.

Como alternativa, en **Ver certificados → Autoridades → Importar**, importa únicamente `LaPrimitiva-Local-Root-CA.cer` y marca la confianza para identificar sitios web. No importes la PFX ni `laprimitiva.local.cer` en Firefox.

> Una excepción manual no sustituye la confianza de cadena. No cierres M-306 mientras Firefox siga mostrando **No seguro**, aunque la aplicación permita continuar.

## 9. Instalar el paquete en otro ordenador local

El segundo equipo debe tener su propia publicación y sitio IIS local. No instales manualmente los ficheros en el primer equipo: `Create` ya lo hizo. Para otro equipo, copia por un canal seguro:

- `LaPrimitiva-Local-Root-CA.cer`.
- `laprimitiva.local.pfx`.

Comunica la contraseña de la PFX por un canal distinto. Para comprobar que la transferencia no alteró los ficheros, compara sus hashes en ambos equipos:

```powershell
Get-FileHash 'C:\Descargas\LaPrimitiva-Local-Root-CA.cer' -Algorithm SHA256
Get-FileHash 'C:\Descargas\laprimitiva.local.pfx' -Algorithm SHA256
```

En el equipo de destino:

1. Repite los pasos 2 a 5 de esta guía.
2. Copia los certificados, por ejemplo, a `C:\Descargas`.
3. Ejecuta como administrador:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\scripts\Manage-M306LocalHttps.ps1' `
  -Action Install `
  -SiteName 'laprimitiva.local' `
  -RootCertificatePath 'C:\Descargas\LaPrimitiva-Local-Root-CA.cer' `
  -PfxPath 'C:\Descargas\laprimitiva.local.pfx'
```

4. Introduce la contraseña de la PFX cuando se solicite.
5. Repite los pasos 7 y 8 para verificar IIS y el navegador.
6. Borra la copia temporal de la PFX cuando ya no sea necesaria.

> El `.cer` de la CA es público. La `.pfx` contiene una clave privada y debe tratarse como un secreto.

## 10. Renovar el certificado

Antes de la caducidad, vuelve a ejecutar `Create` en el equipo que conserva la CA privada:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\scripts\Manage-M306LocalHttps.ps1' `
  -Action Create `
  -SiteName 'laprimitiva.local'
```

Mientras la CA tenga vigencia suficiente, el script la reutiliza, emite un certificado de servidor nuevo, actualiza el binding, elimina los certificados de servidor anteriores y vuelve a exportar el paquete. Después:

1. Ejecuta `-Action Verify`.
2. Redistribuye la PFX renovada a los demás equipos.
3. Sustituye allí el certificado mediante `-Action Install`.

## 11. Retirar HTTPS y los certificados

Para retirar el binding y los certificados de servidor:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\scripts\Manage-M306LocalHttps.ps1' `
  -Action Remove `
  -SiteName 'laprimitiva.local'
```

Para retirar también la CA de los almacenes `LocalMachine\Root` y `LocalMachine\My`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File '.\scripts\Manage-M306LocalHttps.ps1' `
  -Action Remove `
  -SiteName 'laprimitiva.local' `
  -RemoveRoot
```

Retira la CA únicamente cuando ningún otro certificado local dependa de ella.

## 12. Resolución de problemas

### “Ejecuta Windows PowerShell como administrador”

Cierra la consola, abre **Windows PowerShell** con **Ejecutar como administrador** y acepta UAC.

### “Ejecuta este script con Windows PowerShell 5.1”

Estás usando `pwsh`. Ejecuta el comando con `powershell.exe`.

### “No existe el sitio IIS”

Ejecuta `Get-Website` y copia exactamente su propiedad `Name` en `-SiteName`.

### El puerto 443 ya está ocupado

```powershell
Get-NetTCPConnection -State Listen -LocalPort 443 |
  Select-Object LocalAddress, LocalPort, OwningProcess
```

No cambies a un binding comodín para resolver el conflicto. Identifica el sitio o proceso que usa el puerto y conserva SNI.

### El navegador sigue mostrando una advertencia

1. Ejecuta `-Action Verify` y corrige el primer error comunicado.
2. Confirma que la CA está en `Cert:\LocalMachine\Root`.
3. Confirma que el certificado contiene `laprimitiva.local` en `DnsNameList`.
4. Cierra completamente el navegador y vuelve a abrirlo.
5. Comprueba que la fecha y hora del equipo son correctas.

No uses `-SkipCertificateCheck`, `TrustServerCertificate` ni excepciones permanentes del navegador: ocultarían el problema en lugar de solucionarlo.

### Falta la cabecera HSTS

Comprueba que el sitio IIS no fuerza `ASPNETCORE_ENVIRONMENT=Development`, reinicia el pool y vuelve a ejecutar `-Action Verify`.

### Respuesta 400 o 403

- `400`: revisa que la URL use exactamente `laprimitiva.local`.
- `403`: la política M-301 ha detectado un cliente no loopback. No amplíes el binding a la LAN; ejecuta la aplicación y el navegador en el mismo equipo.

## 13. Evidencia para cerrar M-306

Conserva, sin incluir secretos:

1. Salida de `Get-WebBinding` mostrando `127.0.0.1:443:laprimitiva.local` y `SslFlags=1`.
2. Salida del certificado con SAN, vigencia y `HasPrivateKey=True`.
3. Salida correcta de `-Action Verify`.
4. Captura del navegador mostrando `https://laprimitiva.local/` sin advertencias.
5. Confirmación de que `git status --short --ignored -- artifacts` muestra el paquete como ignorado y no versionado.

No adjuntes la PFX, su contraseña ni ninguna clave privada a la evidencia.
