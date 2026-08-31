# Field Note: Review del PR #533

**Fecha**: 2026-08-31
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/533
**Issue**: #530

## Comentarios del review

| # | Categoria | Resumen |
|---|-----------|---------|
| 1 | corregir  | El planner había decidido no extender la suite smoke MCP con el argumento de que el gate local no ejecuta smoke tests; el reviewer humano revirtió esa decisión: el gate solo compila (la key `mcp_extension` se obtiene en runtime por CI, MEF-ADR-0048 seccion 4), y el smoke existente (`ComposicionDelHostSmokeTests`, pin de 4 tools) queda rojo garantizado post-deploy al agregar la 5ª tool. |

## Correcciones aplicadas

- `ComposicionDelHostSmokeTests`: catálogo de `tools/list` actualizado de 4 a 5 tools (`listar_colaboradores` incluida); dos tests nuevos que pinnean su `inputSchema` (`required` vacío y el catálogo de sus 4 parámetros opcionales).
- Archivo nuevo `ListarColaboradoresSmokeTests`: tool call real sin filtro (verifica forma de respuesta contra datos de dev) + error path que afirma el texto exacto de `Mensajes.FechaInvalida`.
- Con esto la tool `listar_colaboradores` cubre las cinco verificaciones canónicas de MEF-ADR-0048 nivel 3 (el hint `readOnlyHint` ya estaba cubierto de forma genérica por un test existente que itera sobre todas las tools).
- Commit `de63d08`, sin tocar código de producción.

## Mejoras a agentes

| Agente | Gap | Destino | Ajuste aplicado |
|--------|-----|---------|------------------|
| planner | regla faltante (DoR no reconoce "tool MCP nueva" como disparador de cobertura smoke) | harness | Ya existe **harness#789** (`estado:borrador`), que cita este mismo PR como evidencia. No se creó draft nuevo para evitar duplicar. |
| smoke-test-writer | regla faltante (doctrina orientada a Functions HTTP, no a tools MCP) | harness | Cubierto por harness#789 (mismo issue, punto 2). |
| reviewer | regla ignorada (acepta el argumento "no hay forma de verificar sin la key de dev", que contradice el diseño) | harness | Cubierto por harness#789 (mismo issue, punto 3). |

## Lecciones

- El argumento "el gate local no ejecuta smoke tests" es evidencia de que **sí se pueden escribir sin key local** (la corrida real es post-deploy en CI vía OIDC), no de que deban omitirse — confundir ambas lecturas es exactamente el gap que harness#789 documenta.
- Antes de crear un draft de mejora al harness, vale la pena buscar en los issues existentes: este caso ya estaba registrado como evidencia de un issue abierto, evitando duplicación.
- El pin de `tools/list` en el nivel 3 (smoke e2e) es una verificación independiente del pin por reflexión del nivel 2 (`ComposicionDelServidorTests`): agregar una tool exige tocar ambos niveles, no solo el que el pipeline TDD toca por defecto.
