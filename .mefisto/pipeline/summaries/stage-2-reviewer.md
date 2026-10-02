# Stage 2 - Reviewer (issue #699)

## Resultado

Aprobado con correcciones. El writer cumple CA-1..CA-4 (identidad de tres datos con fuentes HTTP/Service Bus y obligatoriedad -> 500; nombres Cosmos 3.x en CA-ADR-0032/0027 con la prohibicion de `AgregarTenantContextHibrido()` conservada; orden de despliegue; control de cambios e indice de `CLAUDE.md`). Solo tocó rutas permitidas (`CLAUDE.md`, `docs/adr/ca-adr-*`).

## Correcciones

- CA-ADR-0032: el writer renombro el metodo local `AgregarTenantResolverControlAsistencia()` a `AgregarTenantContextControlAsistencia()`, que no existe en `src/` (los 4 `ComposicionServicios.cs` y `TenancyServiceCollectionExtensions.cs` usan el nombre viejo). Revertido: el rename del issue aplica solo a la API de Cosmos. La nota de nomenclatura ahora aclara que la biblioteca local y su API conservan sus nombres.
- CA-ADR-0032 seccion 2: se añadio que APIM estampa tambien el claim de membresia (#696) y la llave `organization_membership_id` que estampa `TenancyDelivery` en Service Bus.
- CA-ADR-0032 Referencias: se agrego `X-Organization-Membership-Id` a los headers canonicos.

## Verificacion

- Solo cambios de documentacion, sin C#: no aplica build ni tests.
- `grep` en `src/` confirmo el nombre real del metodo local.
- Observacion (del issue): MEF-ADR-0028 (canon del marco, no esta en este repo) puede seguir citando `ITenantResolver`; lo resuelve Mefisto al adoptar Cosmos 3.x. La memoria `proxytenantresolver-roto-en-isolated-worker` sigue con el nombre viejo; actualizarla es tarea humana.
