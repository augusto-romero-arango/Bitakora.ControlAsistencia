# Field Note: Review del PR #180

**Fecha**: 2026-06-23
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/180
**Issue**: #139 (Integrar consolidador DesgloseHoras al flujo reactivo del ControlDiario)

## Comentarios del review

| # | Categoria | Resumen |
|---|-----------|---------|
| 1 | corregir | El valor esperado del test CA-3 (`DesgloseHorasTrasAsignarTurnoTests`) se calculaba ejecutando `ConsolidadorDesgloseHoras.Consolidar(... CalcularDesglose ...)` — la misma logica que el SUT invoca en `RecalcularDesgloseHoras()`. Prueba tautologica: un bug se filtra por igual al esperado y al actual. Reemplazar por oraculo construido a mano. |

## Correcciones aplicadas

- `DesgloseHorasTrasAsignarTurnoTests.cs` (test CA-3): se sustituyo el `esperado` derivado de produccion por un `DesgloseHoras` armado a mano con primitivas del dominio (`new MomentoDelDia`, `IntervaloTemporal.Crear`, `new IntervaloClasificado`, `DetalleRetardo.Crear`, `new DesgloseFranja`, `new DesgloseHoras`), igual que ya hacia el test CA-4 del mismo archivo.
- Escenario verificado a mano: franja 06:00-14:00 en domingo (2026-03-15), trabajado 07:00-15:00 -> retardo 60min `[06:00-07:00]` compensado por excedente 60min `[14:00-15:00]`; ordinaria visible 07:00-14:00 `DominicalFestivaDiurna` (420min), `RetardoNeto = 0`, `FranjasAnomalas = 0`.
- Commit `cd6207a`. Build correcto; 168/168 tests verdes en el proyecto de ControlHoras (sin regresiones).

## Mejoras a agentes

| Agente | Gap | Destino | Ajuste aplicado |
|--------|-----|---------|-----------------|
| test-writer | regla faltante | harness | Draft propuesto en harness #59 |

## Lecciones

- Un oraculo derivado del codigo bajo prueba (o de los colaboradores de produccion que ese codigo invoca) no detecta regresiones: el bug contamina por igual esperado y actual. El valor esperado se construye siempre a mano con factories del dominio.
- El antipatron coexistia con el patron correcto en el mismo archivo: el test CA-4 ya construia su esperado a mano. Esa inconsistencia es señal de que faltaba una regla explicita en el `test-writer`, no de un descuido puntual.
- Construir el oraculo a mano obliga a deducir la "verdad del dominio" del escenario (bandas, tipo de dia, compensacion) de forma independiente, lo que es justo la validacion que la prueba debe aportar.
