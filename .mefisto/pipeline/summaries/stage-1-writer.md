## Implementado
- CA-ADR-0032: nombres Cosmos 3.x, identidad de tres datos (tabla HTTP/Service Bus, obligatorios -> InvalidOperationException -> 500, #666), seccion 5 de orden de despliegue, control de cambios 2026-10-01 (#699).
- CA-ADR-0027: nota de nomenclatura 3.x al inicio (texto historico intacto) y entrada de control de cambios.
- CLAUDE.md: filas de indice de CA-ADR-0027 y CA-ADR-0032 con nombres nuevos.

## Verificacion
Solo documentacion; sin build. Verificadas las aserciones de reemplazo.

## Pendiente/bloqueos
- Observacion: MEF-ADR-0028 puede seguir citando `ITenantResolver`; lo resuelve Mefisto al adoptar Cosmos 3.x.
- La memoria `proxytenantresolver-roto-en-isolated-worker` usa el nombre viejo: tarea humana.
