# M-702 — Comparación histórica de estrategias automáticas

**Fecha:** 24 de agosto de 2026
**Estado:** evaluación experimental; no constituye validación predictiva ni recomendación de gasto.

## Pregunta evaluada

Comparar con el mismo número de apuestas y el mismo coste simulado:

1. selección uniforme;
2. modelo ponderado actual;
3. cobertura diversificada;
4. ensemble temporal con relaciones entre pares.
5. ensemble adaptativo regularizado de nueva creación.

La evaluación cuenta las categorías oficiales de La Primitiva: Especial (6 + reintegro), 1.ª (6), 2.ª (5 + complementario), 3.ª (5), 4.ª (4), 5.ª (3) y reintegro.

## Metodología

- Histórico disponible: 4.178 sorteos entre 1985 y 2026.
- Entrenamiento inicial: 104 sorteos.
- Evaluación walk-forward: 4.074 sorteos, sin utilizar resultados futuros.
- Cartera: 5 apuestas por sorteo y estrategia.
- Total: 20.370 apuestas por estrategia y 20.370 € de coste simulado a 1 € por apuesta.
- Semillas independientes por sorteo y reproducibles.
- El nuevo ensemble mezcla cuatro expertos —uniforme y vidas medias de 90, 365 y 1.825 días—. Antes de cada sorteo pondera sus probabilidades según el Brier acumulado hasta ese momento, aplica un 20 % de contracción hacia uniforme y diversifica la cartera. Solo actualiza los pesos después de observar el resultado, por lo que no existe fuga futura.
- El mismo reintegro simulado se asigna a todas las estrategias, porque el jugador no lo elige: lo asignan los sistemas centrales de SELAE.
- `1.ª (6)` cuenta todos los boletos con seis aciertos; `Especial` es el subconjunto que también acierta el reintegro.

La comparación se reproduce con `scripts/Invoke-M702StrategyComparison.ps1`. La evidencia completa está en `mejoras/evidencias/M-702-strategy-comparison-20260824.json`.

## Resultados por categoría

| Estrategia | Especial | 1.ª | 2.ª | 3.ª | 4.ª | 5.ª | Premios principales | Reintegro | Cualquier premio |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Uniforme | 0 | 0 | 0 | 0 | 26 | 342 | 368 | 2.041 | 2.364 |
| Ponderado actual | 0 | 0 | 0 | 1 | 20 | 350 | 371 | 2.041 | 2.372 |
| Cobertura diversificada | 0 | 0 | 0 | 0 | 17 | 367 | **384** | 2.041 | 2.380 |
| Ensemble temporal y pares | 0 | 0 | 0 | 0 | 23 | 361 | **384** | 2.041 | **2.386** |
| Ensemble adaptativo regularizado | 0 | 0 | 0 | 0 | 25 | 351 | 376 | 2.041 | 2.385 |

## Distribución completa de coincidencias

| Estrategia | 0 | 1 | 2 | 3 | 4 | 5 | 6 | Media por apuesta |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Uniforme | 8.923 | 8.412 | 2.667 | 342 | 26 | 0 | 0 | 0,730290 |
| Ponderado actual | 8.869 | 8.395 | 2.735 | 350 | 20 | **1** | 0 | 0,736377 |
| Cobertura diversificada | 8.807 | 8.500 | 2.679 | **367** | 17 | 0 | 0 | **0,737703** |
| Ensemble temporal y pares | 8.815 | 8.515 | 2.656 | 361 | 23 | 0 | 0 | 0,736475 |
| Ensemble adaptativo regularizado | 8.862 | 8.412 | 2.720 | 351 | 25 | 0 | 0 | 0,736622 |

## Significancia

| Estrategia | Z pareado de premios principales frente a uniforme | ¿Supera ±1,96? |
|---|---:|---|
| Ponderado actual | 0,110643 | No |
| Cobertura diversificada | 0,593769 | No |
| Ensemble temporal y pares | 0,578812 | No |
| Ensemble adaptativo regularizado | 0,303642 | No |

Cobertura y ensemble consiguieron 16 premios principales más que la selección uniforme, pero la diferencia entra dentro de la variabilidad esperable. **Ninguna estrategia ha demostrado una ventaja predictiva estadísticamente convincente.**

## Nuevo algoritmo: ensemble adaptativo regularizado

El algoritmo se diseñó para que pudiera descubrir de forma online qué escala temporal funcionaba mejor sin elegirla a posteriori:

1. cuatro expertos producen probabilidades antes de cada sorteo: uniforme y frecuencias con vidas medias de 90, 365 y 1.825 días;
2. se combinan mediante pesos exponenciales, penalizando después de cada sorteo a los expertos con mayor pérdida Brier;
3. la mezcla se contrae un 20 % hacia la distribución uniforme para limitar el sobreajuste;
4. se generan candidatas ponderadas y se eligen cinco minimizando su solapamiento.

El resultado fue **376 premios principales**, ocho más que uniforme pero ocho menos que cobertura y ensemble temporal/de pares. Alcanzó como máximo cuatro aciertos y su diferencia pareada contra uniforme fue `z = 0,303642`: no significativa.

Lo más informativo no es el recuento de premios, sino el diagnóstico interno. Tras 4.074 actualizaciones, los pesos finales fueron: uniforme `0,99999139`, ventana de 1.825 días `0,00000861` y prácticamente cero para 90 y 365 días. Sus pérdidas Brier medias fueron respectivamente `0,10745523`, `0,10759837`, `0,10842303` y `0,10781297`. Es decir, el propio algoritmo aprendió que **la distribución uniforme calibraba mejor que las tres ventanas históricas**.

Este ensayo es exploratorio porque el diseño se creó después de ver la comparación anterior. No puede considerarse una confirmación independiente: cualquier candidato futuro deberá congelarse y validarse prospectivamente o sobre un periodo final intacto.

La regla de actualización multiplicativa se apoya en el marco de aprendizaje online de Freund y Schapire, y la calidad probabilística se evalúa con la puntuación de Brier: [Freund y Schapire (1997)](https://doi.org/10.1006/jcss.1997.1504) y [Brier (1950)](https://doi.org/10.1175/1520-0493(1950)078%3C0001:VOFEIT%3E2.0.CO;2).

## Por qué el histórico no permite elegir un predictor del premio mayor

Con 20.370 apuestas por estrategia, los recuentos teóricos aproximados son:

| Categoría | Probabilidad oficial aproximada | Casos esperados en el backtest |
|---|---:|---:|
| Especial | 1 entre 139.838.160 | 0,000146 |
| 1.ª | 1 entre 13.983.816 | 0,001457 |
| 2.ª | 1 entre 2.330.636 | 0,008740 |
| 3.ª | 1 entre 55.491 | 0,367087 |
| 4.ª | 1 entre 1.032 | 19,738372 |
| 5.ª | 1 entre 57 | 357,368421 |
| Reintegro | 1 entre 10 | 2.037 |

Que no aparezca ningún pleno no es un fallo del backtest: es lo matemáticamente esperable. Harían falta aproximadamente 13,98 millones de apuestas para esperar un solo pleno de seis y 139,84 millones para esperar un Especial. Aumentar artificialmente las simulaciones no crea información predictiva; solo simula más gasto.

Fuente de categorías y probabilidades: [SELAE — Cómo jugar a La Primitiva](https://www.loteriasyapuestas.es/es/centro-de-ayuda/como-se-juega/jugar-a-la-primitiva).

## Decisión final para una apuesta semanal

Para una única apuesta automática por semana se adopta **selección uniforme sin reemplazo**. La cobertura diversificada solo aporta una diferencia operativa cuando existe una cartera de varias apuestas, y ningún modelo histórico demostró ventaja predictiva. El generador permite regenerar otra candidata uniforme y no excluye combinaciones ganadoras anteriores: el histórico contiene una repetición real, `13-21-24-26-32-34`, en 2002 y 2009.

La aplicación debe describir esta función como generación automática, nunca como una combinación ganadora o predicción. Cada candidata válida mantiene una probabilidad de 1 entre 13.983.816 de acertar los seis números.

## Conclusión del experimento

- **Premios de categorías bajas:** cobertura y ensemble quedaron ligeramente por delante, pero empataron y sin significancia.
- **Mejor categoría alcanzada:** el ponderado actual produjo el único caso de cinco aciertos; un único caso no demuestra superioridad.
- **Mayor dispersión de una cartera:** cobertura cumple mejor este objetivo por diseño, aunque no predice el pleno.
- **Predicción del premio mayor:** ninguna estrategia aporta evidencia de ventaja.
- **Nuevo algoritmo adaptativo:** no mejora a las mejores carteras anteriores y converge casi por completo a uniforme; este resultado negativo es evidencia útil contra explotar frecuencias históricas.

No debe elegirse todavía un nuevo predictor basándose en estos recuentos. El siguiente experimento válido es repetir semillas, separar periodos temporales finales y aplicar corrección por comparación de múltiples modelos. Si ninguna ventaja se mantiene, la aplicación debe presentar la automática como generación diversificada, no como predicción.

## ¿Existe un proceso estadístico mejor?

Existe un proceso **mejor para comprobar hipótesis**, no un algoritmo conocido que permita predecir de forma fiable un sorteo justo:

1. preregistrar modelos, parámetros y métricas antes de mirar el periodo final;
2. usar validación walk-forward anidada y un holdout temporal intacto;
3. repetir las estrategias estocásticas con muchas semillas y publicar intervalos de confianza;
4. corregir por las múltiples variantes probadas para evitar falsos positivos;
5. comparar la probabilidad posterior de cada modelo contra la hipótesis uniforme;
6. abandonar cualquier modelo cuya ventaja no sea estable entre máquinas, periodos y tipos de sorteo.

Solo tendría sentido intentar predecir si existiera evidencia independiente de un sesgo físico o procedimental —por ejemplo, información de máquina, juego de bolas, mantenimiento o condiciones de extracción—. Con únicamente la secuencia histórica de números, un sorteo independiente y uniforme no ofrece señal explotable para anticipar la combinación ganadora.
