# Field Note: Review del PR #155

**Fecha**: 2026-04-26
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/155
**Issue**: #115 (Segmentar intervalo trabajado por fronteras horarias legales)

## Comentarios del review

| # | Categoria  | Resumen                                                                          |
|---|------------|----------------------------------------------------------------------------------|
| 1 | corregir   | `IntervaloTemporal.MinutosAbsolutosInicio` rompe encapsulamiento del VO          |
| 2 | corregir   | `SegmentadorHorario.ObtenerFronterasInternas` viola Tell-don't-Ask sobre el VO   |
| 3 | corregir   | `SegmentadorHorario` como clase estatica externa es innecesaria                  |
| 4 | corregir   | Test de `MinutosAbsolutosInicio` no debe existir (consecuencia del #1)           |
| 5 | corregir   | Segundo test de `MinutosAbsolutosInicio` no debe existir                         |
| 6 | corregir   | Tests de segmentacion deben vivir junto al SUT (en el VO, no en clase externa)   |

## Correcciones aplicadas

Commit `9b0733e` en la rama del PR (`worktree-issue-115-segmentar-intervalo-trabajado-por-fronte`):

- `IntervaloTemporal.Segmentar(IEnumerable<TimeOnly>)` agregado al VO en `Contracts` como operacion geometrica pura.
- `IntervaloTemporal.MinutosAbsolutosInicio` eliminado.
- `SegmentadorHorario.cs` eliminado.
- `FronterasHorariasLegales` enriquecido con `Medianoche` y `Todas` (combinacion canonica de fronteras legales).
- Los 8 CAs migrados a `IntervaloTemporalSegmentacionTests.cs` en `Contracts.Tests`, pasando las fronteras como literales `TimeOnly` para reforzar que el metodo es geometria pura.
- Tests de `MinutosAbsolutosInicio` eliminados.

Resultado: 306/306 tests verde tras la migracion.

## Causa raiz del review

El reviewer humano detecto que el implementer expuso una propiedad publica nueva (`MinutosAbsolutosInicio`) sobre `IntervaloTemporal` para que un servicio externo (`SegmentadorHorario`) pudiera operar sobre el. El implementer documento la decision como "desviacion del plan" en su resumen y el reviewer interno la aprobo. El reviewer humano la rechazo: la operacion debio moverse al propio VO (Tell-don't-Ask, ADR-0015), no exponer estado interno.

Este es el patron documentado tambien en PR #142 y PR #144 (violaciones a ADR-0015 que pasaron review). La causa comun: el principio Tell-don't-Ask estaba **documentado pero no operacionalizado** como filtro activo de decision en los agentes.

## Mejoras a agentes (commits separados a `main`)

### Commit `3ab371e` — Visibilizar (CLAUDE.md + ADR-0015)

| Archivo       | Gap                                                                                                  | Ajuste aplicado                                                                                                                       |
|---------------|------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| CLAUDE.md     | Indice tematico canalizaba encapsulamiento bajo "Serializacion, value objects con ctor privado"      | Agregada entrada propia "Encapsulamiento, Tell-don't-Ask, ocultacion de estado interno (aplica por igual a aggregates y a VOs)"       |
| ADR-0015      | La seccion "Tell Don't Ask" usaba ejemplos solo de aggregates; el patron quedaba ambiguo para VOs    | Extendida con parrafo explicito "aplica por igual a aggregates y VOs", ejemplo negativo con caso real (PR #155), heuristica de 3 preguntas |

### Commit `1c413ab` — Operacionalizar (planner + implementer + reviewer + test-writer)

| Agente        | Gap                                                                                                                | Ajuste aplicado                                                                                                                                  |
|---------------|--------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| planner       | Tabla "Puntos de corte" normalizaba "clase estatica" como opcion neutra de testabilidad                            | Reformulado: clase estatica es ultimo recurso; antes verificar si un VO/aggregate existente puede absorber la operacion                          |
| planner       | Checklist pre-listo no obligaba a verificar la API actual del VO antes de proponer un algoritmo que la consuma     | Dos casillas nuevas: Tell-don't-Ask explicito + verificacion de API existente (decision tomada por el planner, no delegada al implementer)       |
| implementer   | Formato "Desviaciones de ADRs" aceptaba documentacion como prueba suficiente, sin exigir alternativas exploradas   | Campo obligatorio "Alternativas exploradas y descartadas" cuando la desviacion expone estado. Si no se logra articular alternativa, detenerse y reportar gap |
| reviewer      | Auditoria de ADRs reactiva, sin checklist concreto de antipatrones                                                 | Subseccion "Antipatrones de ADR-0015 a detectar activamente" con 6 items, incluyendo caso PR #155                                                 |
| test-writer   | Aceptaba "Interfaz publica propuesta" del planner sin auditarla contra Tell-don't-Ask                              | Auditoria activa con 3 preguntas antes de crear stubs; instruccion de "Cuestionamiento al plan del planner" cuando se detecta exposicion innecesaria |

## Lecciones

- **Hablar del principio no es operacionalizarlo.** ADR-0015 existia desde hace meses y mencionaba Tell-don't-Ask. Pero ningun agente tenia un filtro activo (checklist, pregunta obligatoria, campo obligatorio en plantilla) que aplicara el principio en su flujo de decision. El sesgo del pre-entrenamiento — que tiende a exponer getters cuando la API no soporta una operacion — gano por defecto en cada slot donde no habia guardrails.
- **El indice tematico de CLAUDE.md es la primera defensa cognitiva.** Si el agente busca "encapsulamiento" o "Tell-don't-Ask" y no lo encuentra como entrada propia, infiere que el tema es marginal. Una entrada del indice cuesta una linea y cambia el peso percibido del principio.
- **Las decisiones arquitectonicas no se delegan al implementer como "desviaciones".** Si el plan describe un algoritmo que requiere acceso a propiedades del VO que no existen, esa es una decision que el planner debe tomar — ampliar API o mover la operacion al VO — antes de pasar al pipeline. El implementer no esta posicionado para resolverla bajo presion de hacer pasar los tests.

---

## Round 2 (post primera respuesta del review)

Tras publicar las respuestas y los commits del round 1, el reviewer humano dejo dos hallazgos adicionales.

### Comentario 7 (id 3144334111 + clarificacion 3144337921): valor semantico en expresion aritmetica

> "la expresion de esas operaciones tiene un valor semantico para el dominio que seria correcto abstraer en un field, no crees? — `t => dia * MinutosPorDia + t.Hour * MinutosPorHora + t.Minute`"

La expresion estaba **duplicando** lo que `MomentoDelDia.MinutosAbsolutos` ya hace. Mismo Tell-don't-Ask de los hilos anteriores, aplicado un nivel mas adentro: en lugar de recalcular el valor a mano dentro de `IntervaloTemporal`, le pedimos al VO que ya sabe.

Commit `36a7380` en la rama del PR:

- `t => dia * MinutosPorDia + t.Hour * MinutosPorHora + t.Minute` -> `t => new MomentoDelDia(t, dia).MinutosAbsolutos`.
- `inicioMin / MinutosPorDia` -> `_inicio.DiaOffset` (idem `_fin`).
- Eliminada la constante privada `MinutosPorDia` de `IntervaloTemporal` (vivia duplicada con la de `MomentoDelDia`).

### Hallazgo derivado: `FronterasHorariasLegales` quedo huerfano

Tras el refactor de Tell-don't-Ask + el del comentario 7, se hizo evidente que `FronterasHorariasLegales` no tenia consumidores reales en el PR. Las unicas tres apariciones eran su propia definicion y dos comentarios documentales. La clase se habia creado "para que #134/#136 la usaran" — antipatron explicitamente proscrito en `planner.md` seccion "Cuando NO partir":

> VO o clase huerfana entre PRs: si el corte deja una clase sin consumidor en el PR donde se crea, queda codigo muerto hasta que el siguiente PR la use.

Commit `4834c04` en la rama del PR: eliminada `FronterasHorariasLegales.cs` y limpiados los comentarios que la referenciaban. La HU que la requiera (#134 o #136) la creara con la firma exacta que su primer consumidor real necesite.

### Mejoras adicionales a agentes y ADR

| Archivo / Agente   | Gap                                                                                                                                                                                                | Ajuste aplicado                                                                                                                                                                                                              |
|--------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| planner.md         | La regla "VO o clase huerfana entre PRs" vivia en la seccion "Cuando NO partir" como parrafo de contexto, no como filtro operativo en el checklist pre-listo                                       | Casilla nueva: "Sin artefactos huerfanos: cada clase/archivo listado en 'Impacto / Crea' tiene al menos un consumidor real en el mismo PR". Cita el caso real de `FronterasHorariasLegales`.                                  |
| implementer.md     | La seccion "Tell Don't Ask" cubria solo la cara prohibitiva (no expongas estado); no orientaba al consumo activo de la API publica del objeto antes de escribir aritmetica sobre sus propiedades   | Subseccion nueva "Aprovechar la superficie del dominio (pre-flight)": antes de combinar propiedades primitivas de un VO/aggregate, leer la API publica completa y usar la propiedad/metodo derivado si ya existe. Caso real PR #155 round 2. |
| ADR-0015           | El principio Tell-don't-Ask se documentaba solo como prohibicion ("no expongas estado para que servicios externos operen"); faltaba la cara positiva                                               | Parrafo nuevo "Contrapartida activa: aprovechar la superficie del dominio". Articula que un objeto rico solo es rico si sus consumidores aprovechan su superficie; conecta con deep modules de Ousterhout. Ejemplo del caso real. |

### Lecciones adicionales

- **Tell-don't-Ask es fractal.** El round 1 movio la operacion al VO. El round 2 movio el calculo intermedio al VO de mas adentro. Cada nivel de abstraccion tiene su propia oportunidad de aplicar el principio — no basta resolverlo "una vez" en el PR.
- **Eliminar codigo muerto vale tanto como agregar correcto.** Mantener `FronterasHorariasLegales` "preparado para el futuro" se sintio como buena ingenieria, pero era YAGNI — la HU que la requiera la creara con la firma que necesite, no con la que adivinamos hoy.
- **El round 2 demuestra el valor del review humano repetido.** El round 1 cerro con respuestas publicadas y mejoras a agentes. El reviewer humano leyo de nuevo y encontro otra capa. La "señal de termino" del review no es "respondi todos los comentarios" — es "el reviewer humano lo da por cerrado".
- **Leccion mas importante del review entero — un objeto rico solo es rico si se consume.** El proyecto invirtio en `MomentoDelDia` con `MinutosAbsolutos` como propiedad de dominio (issue #143). Esa inversion se desperdicio en el momento en que `IntervaloTemporal.Segmentar` reescribio la formula a mano. La consecuencia no fue solo duplicacion: cuando el codigo habla aritmetica primitiva (`x * 1440 + y * 60 + z`) en lugar del lenguaje del dominio (`MinutosAbsolutos`), el modelo desaparece donde mas deberia estar visible. **Tell-don't-Ask tiene dos caras: "no expongas getters" (prohibitiva) y "consume la riqueza expuesta" (activa).** Ambas requieren la misma practica de fondo: leer la API publica del objeto antes de escribir codigo sobre el. Sin eso, los deep modules quedan atrapados; con eso, el lenguaje del dominio se mantiene vivo en cada sitio de consumo.
