# CA-ADR-0040: Eliminacion de datos de smoke que pueden volver fragil al test

## Estado
Aceptado (2026-10-12). Precisa MEF-ADR-0013 (smoke tests contra dev). Origen: issue #952. Enmendado por #953 (tenant desechable) y por #992 (tenant propio por test, nunca borrado).

## Contexto
MEF-ADR-0013 aisla los datos de cada smoke con IDs unicos (`Guid.CreateVersion7()`) y no exige cleanup. Eso basta cuando la unicidad es por llave. Una Jornada, en cambio, se rechaza con 409 si sus limites (horas semanales, tope diario, minimo diario, dias de descanso) igualan los de otra del tenant, incluidas las desactivadas (#869). La restriccion es por contenido: dos jornadas con GUIDs distintos chocan igual. Los smoke de Programacion generaban limites con `Random` (59 combinaciones), las jornadas se acumulaban en dev y el run de CI fallaba con 409 en 9 de 14 casos.

## Decision
1. **Unicidad por llave**: el aislamiento por GUID de MEF-ADR-0013 es suficiente; no hay cleanup.
2. **Restriccion por contenido (o singleton compartido): tenant propio por test, nunca borrado** (#992). Todo test que crea Jornadas o toca Preferencias corre en un tenant `tenant-smoke-{guid}` nuevo, con su propio `HttpClient` (headers `X-Tenant-Id`, `X-User-Id`, `X-Organization-Membership-Id`; la identidad llega por headers confiables sin registro previo del tenant, CA-ADR-0027/CA-ADR-0032). La unicidad de limites se evalua por tenant y, con `TenancyStyle.Conjoined`, la PK de `mt_streams` es `(tenant_id, id)`: un tenant nuevo no tiene firmas previas y admite GUIDs fijos de stream. Sin Postgres configurado, los tests que lo leen se omiten.
3. **Firmas reservadas**: la firma compartida entre suites (hoy 42 h / 8 h / 0 / 1 descanso, la de la jornada predeterminada de `tenant-smoke` que reutilizan ControlHoras y el MCP) es reservada: ninguna entrada del catalogo puede igualarla. El catalogo (`Fixtures/LimitesDePrueba.cs`) sigue como fuente de limites validos y distintos dentro de un mismo test.
4. **Alcance vigente**: jornadas y el singleton por tenant `PreferenciasProgramacion` (`pp:{tenantId}`) de `Programacion.SmokeTests`. Extender la politica a otro dato requiere su propio issue.
5. **Predeterminada**: ningun test de Programacion modifica la Jornada predeterminada ni el stream `pp:tenant-smoke`; cada tenant propio obtiene la suya por `AseguradorJornadaPredeterminada` al primer uso. `tenant-smoke` se conserva para los tests aislados por GUID y para ControlHoras/MCP.
6. **Los smoke nunca borran datos de Marten**: ningun fixture ejecuta `DELETE`/`TRUNCATE` sobre `mt_events`, `mt_streams` ni documentos de Marten. Postgres queda de solo lectura (aserciones sobre eventos y vistas). Origen: el 2026-10-13 los `DELETE` de los fixtures dejaron huecos en la secuencia de eventos y el daemon de proyecciones quedo detenido en el hueco 1590 (21 smoke fallaron, run 38416139087).

## Consecuencias
- Los smoke de jornadas son deterministas: dos runs seguidos no colisionan, sin purga previa ni borrado final.
- Residuo aceptado: los tenants `tenant-smoke-*` quedan en dev, aislados por tenant. Su higiene periodica, sin borrar eventos en caliente, se define en #994.
- La tolerancia del daemon a huecos legitimos de secuencia se trata en #993.
- Un test con tenant propio no depende del orden de ejecucion ni del estado de `tenant-smoke`.
