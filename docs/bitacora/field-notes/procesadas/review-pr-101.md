# Field Note: Review del PR #101

**Fecha**: 2026-04-14
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/101
**Issue**: #99

## Comentarios del review

| # | Categoria | Resumen |
|---|-----------|---------|
| 1 | corregir  | El test del happy path usaba 1 fecha y verificaba 1 evento. Debe usar al menos 2 fechas para verificar fan-out. |

## Correcciones aplicadas

- `DebePublicarProgramacionTurnoDiarioSolicitada_CuandoSolicitudEsAceptada`: cambiado de 1 fecha a 2 fechas, consume 2 eventos con predicado amplio por SolicitudId, verifica fechas con BeEquivalentTo.

## Mejoras a agentes

| Agente            | Gap            | Ajuste aplicado |
|-------------------|----------------|-----------------|
| smoke-test-writer | regla faltante | Agregar regla #4 "Fan-out de arreglos": cuando el payload contiene un arreglo que produce fan-out de eventos, el happy path debe enviar al menos 2 elementos. |

## Lecciones

- Testear con un solo elemento en un arreglo que produce fan-out no distingue "emite 1 evento" de "emite N eventos". Siempre usar N>1 para verificar fan-out real.
- El writer cumplio los CAs al pie de la letra -- el gap estaba en la falta de una heuristica general, no en un CA mal escrito.
