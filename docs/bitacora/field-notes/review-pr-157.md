---
fecha: 2026-04-26
hora: 21:16
sesion: fix-review
tema: Cobertura de smoke tests cuando un feature publica a un topic sin suscripcion smoke-tests
---

# Field Note: Review del PR #157

**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/157
**Issue**: #108 — Emitir DiaCalculado tras adicionar marcacion

## Comentarios del review

| # | Categoria | Resumen |
|---|-----------|---------|
| 1 | corregir  | "Por que no se generaron smoke tests de este feature?" — falta cobertura de los efectos del handler in-process `AdicionarMarcacionCuandoMarcacionRegistrada` (persistencia de `marcacion_adicionada` y publicacion de `DiaCalculado`). |

## Correcciones aplicadas (commit dc9feb0 en la rama del PR)

- `infra/environments/dev/main.tf`: anadida suscripcion `smoke-tests` al topic `dia-calculado` con TTL 5m.
- `tests/.../ControlHoras.SmokeTests/Fixtures/ServiceBusFixture.cs`: portado `WaitForMessageAsync<T>` desde el fixture de Programacion.
- `tests/.../RegistrarMarcacionFunction/RegistrarMarcacionSmokeTests.cs`: nuevo test `DebePublicarDiaCalculadoYPersistirMarcacionAdicionada_CuandoMarcacionGeneraNuevoEvento` con setup de `TurnoDiarioAsignado` previo, purga, POST y verificacion triple (Postgres + SB + dead letters vacios).

Build verde (349/349 unit tests + 5 smoke omitidos por ausencia de Postgres dev local).

## Mejoras a agentes (commit pendiente en main)

| Agente | Gap | Ajuste aplicado |
|---|---|---|
| reviewer | regla ignorada (cobertura por efecto, no por status global del topic) | Anadido bullet en seccion "Smoke tests (post-#23)": evaluar cada efecto independientemente, no marcar `n/a` cuando solo la publicacion no es verificable. Caso real PR #157 documentado. |
| planner | regla faltante (provision de suscripcion smoke-tests) | Anadido item al checklist pre-listo: si el feature introduce evento publico nuevo o publicacion a topic existente, el alta de la suscripcion `smoke-tests` va listada en `## Impacto en archivos` del mismo issue, sin diferir. |
| implementer | regla faltante (suscripcion smoke-tests siempre presente) | Reescrita seccion "Infraestructura (topics y subscriptions)" para exigir `smoke-tests` con TTL 5m en cada topic, incluso sin consumidores. Caso real PR #157 documentado. |

## Lecciones

- **"El topic no tiene subscriptions" no es excusa para omitir smoke tests.** Los efectos verificables via Postgres (persistencia) son siempre cubribles, y la suscripcion `smoke-tests` se puede agregar en el mismo PR con costo trivial (TTL 5m, sin filtro).
- **Cobertura por efecto, no por estado global**. Cuando un handler gana un nuevo efecto secundario, cada efecto se evalua independientemente: persistencia (siempre verificable), publicacion a SB (depende de suscripcion), envio a queue (futuro). Marcar el conjunto como `n/a` por bloqueo parcial es un anti-patron.
- **El planner es responsable de listar la suscripcion smoke-tests cuando el feature publica.** No es responsabilidad del implementer adivinar; es input contractual del issue. Documentado en el checklist pre-listo.
- **Operacional**: cuando el PR mezcla cambio de infra (`infra/**`) con cambio de codigo del dominio, los workflows `infra-cd` y `deploy-{dominio}` se disparan en paralelo. Existe riesgo de carrera donde el smoke test corre antes de que la suscripcion exista. Solucion ad-hoc: relanzar el job; solucion estructural (futura): reordenar la cadena de workflows o agregar `workflow_dispatch` a `infra-cd`.
