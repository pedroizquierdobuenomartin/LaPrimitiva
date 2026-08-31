# Estrategia de localización

## Cultura inicial

La aplicación soporta inicialmente una única cultura: `es-ES`. `Program.cs` configura la misma cultura para presentación (`CurrentCulture`) e interfaz (`CurrentUICulture`) antes de renderizar componentes. El atributo `lang` del documento toma el valor de la cultura activa.

## Organización de catálogos

Los recursos se separan por responsabilidad funcional; no existe un catálogo monolítico ni un archivo por componente:

| Catálogo | Responsabilidad |
| --- | --- |
| `GlobalResource` | Acciones y términos compartidos entre varias áreas. |
| `LayoutResource` | Navegación estructural, paneles laterales, notificaciones y accesibilidad del layout. |
| `ReconnectionResource` | Estados de reconexión y reanudación de la sesión Blazor. |
| `ErrorResource` | Presentación segura de errores, referencias y códigos estables de M-506. |
| `DashboardResource` | Dashboard y métricas. |
| `RegistrationResource` | Registro y edición semanal. |
| `PlansResource` | Gestión de planes. |
| `HistoricalResource` | Resultados históricos. |
| `CombinationResource` | Combinación automática y backtest. |
| `DataResource` | Exportación e importación de datos. |
| `HelpResource` | Ayuda. |
| `PrivacyResource` | Privacidad. |
| `TermsAndConditionsResource` | Términos y condiciones. |

Cada marcador `*Resource.cs` dispone de un catálogo `*.es-ES.resx`. No se mezclan recursos `.es.resx` neutrales con recursos específicos de España.

## Claves ausentes y fallback

`RequiredStringLocalizerFactory` envuelve el localizador estándar. Si una clave no puede resolverse para la cultura activa, lanza `MissingLocalizationResourceException`; nunca presenta silenciosamente la clave técnica como texto final.

La cultura soportada está limitada a `es-ES`. Para añadir otro idioma se deben incorporar todos sus catálogos y habilitarlo explícitamente en `LocalizationConfiguration`. Una cultura incompleta no se publica ni se selecciona mediante fallback accidental.

## Fronteras culturales

- La UI formatea fechas, números y moneda mediante la cultura activa.
- Los importes permanecen como `decimal`; EF Core conserva precisión y escala y SQL Server utiliza columnas `decimal`.
- Las fechas de negocio permanecen tipadas; no se persisten representaciones localizadas.
- CSV utiliza decimales invariantes y fechas ISO `yyyy-MM-dd`.
- RSS interpreta las fechas conforme a su contrato con `InvariantCulture`.
- Los valores geométricos de SVG, rutas, nombres SQL, claves de configuración, identificadores de log y protocolos web permanecen técnicos o invariantes.
- Los controles HTML `number` y `date` pueden usar internamente el protocolo normalizado del navegador; ese protocolo no es una cadena de presentación ni se reutiliza como exportación.

## Exclusiones revisadas del inventario

El verificador no considera texto traducible:

- la marca `La Primitiva Audit` y los símbolos oficiales `C`, `R` y `Joker` cuando identifican datos del sorteo;
- rutas públicas, nombres de clases CSS, identificadores HTML y nombres de eventos;
- SVG, formatos de archivos, URLs y mensajes exclusivamente diagnósticos;
- contenido histórico o introducido por la persona usuaria.

Toda etiqueta, ayuda, validación presentada, estado vacío, título, acción o atributo de accesibilidad queda fuera de estas exclusiones y debe proceder de un catálogo.

## Incorporación de un idioma futuro

1. Crear un catálogo de la nueva cultura para cada marcador de la tabla.
2. Verificar que todas las claves de `es-ES` tienen traducción equivalente.
3. Añadir la cultura a `SupportedCultures` y `SupportedUICultures`.
4. Ejecutar las pruebas de resolución, claves ausentes, formatos UI y contratos invariantes.
5. Revisar manualmente navegación, páginas, errores, reconexión y vistas responsive antes de habilitarla.
