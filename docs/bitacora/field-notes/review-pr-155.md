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
