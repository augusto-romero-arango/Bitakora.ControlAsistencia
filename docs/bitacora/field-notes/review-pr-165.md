# Field Note: Review del PR #165

**Fecha**: 2026-06-17
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/165
**Issue**: #131

## Comentarios del review

| # | Categoria | Resumen |
|---|-----------|---------|
| 1 | corregir  | El reviewer prefiere condiciones en positivo: `if(existe)` en vez de `if(!existe)`. |

## Correcciones aplicadas

- `AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler`: se invirtio la guarda del patron crear-o-actualizar a la forma afirmativa (`if (existe)`) y se permutaron las ramas del `if/else` (la rama positiva hace `GetAggregateRootAsync` + `AsignarTurno`; el `else` hace `Iniciar` + `StartStream`). Comportamiento identico. Commit `6984a79`. Build correcto, 110/110 verdes en `ControlHoras.Tests`.

## Mejoras a agentes

La mejora no se aplico en este repo: tras la extraccion del harness, los archivos de agentes viven en el plugin `mefisto` (read-only, versionado), no en `.claude/agents/`. Se levanto issue de seguimiento en el repo del harness.

| Agente      | Gap            | Ajuste propuesto (issue harness #37) |
|-------------|----------------|--------------------------------------|
| implementer | regla faltante | Preferir condiciones en positivo (`if(existe)` sobre `if(!existe)`); ordenar `if/else` para que la guarda sea afirmativa. Excepcion: guard clauses de early-return. Verificacion a cargo del reviewer. |

Issue: https://github.com/augusto-romero-arango/eda-evsourcing-azure-harness/issues/37

## Lecciones

- El implementer replico mecanicamente el patron crear-o-actualizar de un precedente (#108) y arrastro su guarda negada. El gap no fue un CA mal escrito sino la ausencia de una convencion de estilo sobre el sentido de las condiciones.
- Con el harness ya extraido al plugin, la Fase 5.4 de `/fix-review` (editar agentes en la rama del PR) deja de aplicar para mejoras de agentes: el destino correcto es un issue/PR en el repo del harness. La field note sigue viviendo en el repo consumidor por su valor historico local.
