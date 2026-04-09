# Field Note: Review del PR #20

**Fecha**: 2026-04-09
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/20
**Issue**: #18

## Comentarios del review

| # | Categoria   | Resumen                                                    |
|---|-------------|------------------------------------------------------------|
| 1 | corregir    | No usar DateTime.UtcNow para fechas en smoke tests         |
| 2 | corregir    | Filtrar eventos por SolicitudId, no solo por existencia    |
| 3 | corregir    | No asumir posicion del evento con eventos[^1]              |
| 4 | investigar  | Naming generico de suscripcion Service Bus smoke tests     |

## Correcciones aplicadas

- Fecha fija `new DateOnly(2026, 4, 9)` en vez de `DateTime.UtcNow.AddDays(30)`
- `ExisteEventoAsync` y nuevo `ObtenerEventoAsync` filtran por campo JSON (`SolicitudId`)
- PostgresFixture encapsula busqueda internamente con metodo privado `ObtenerEventosInternoAsync`
- Referencia a Contracts para comparar value objects con igualdad natural de records
- Creado issue #22 para naming de suscripciones

## Mejoras a agentes

| Agente/Skill      | Gap              | Ajuste aplicado                                              |
|-------------------|------------------|--------------------------------------------------------------|
| smoke-test-writer | regla faltante   | Fechas fijas, nunca DateTime.UtcNow                          |
| smoke-test-writer | regla faltante   | Fixtures no exponen colecciones, filtrar por ID del evento   |
| smoke-test-writer | regla estricta   | Permitir referencia a Contracts para aserciones              |
| fix-review        | api incorrecta   | Endpoint correcto: POST /pulls/{pr}/comments con in_reply_to |

## Lecciones

- Los smoke tests de eventos necesitan filtros por ID unico para evitar falsos positivos por race conditions entre tests paralelos
- "No referenciar produccion" era demasiado estricto — los Contracts (value objects compartidos) son seguros para aserciones
- El endpoint de GitHub para responder review comments es `POST /pulls/{pr}/comments` con `-F in_reply_to=<id>`, no un sub-endpoint `/replies`
