# Servidor MCP de Comandos -- ControlAsistencias

Servidor [MCP](https://modelcontextprotocol.io/) remoto del bounded context, desplegado como
Azure Functions con la extension `Microsoft.Azure.Functions.Worker.Extensions.Mcp`
(MEF-ADR-0047). Cliente HTTP puro de las Function Apps del BC -- cero `ProjectReference` hacia
ningun proyecto del BC.

## Proposito y limites

- **Un servidor MCP por Bounded Context y por proposito**, nunca por dominio (MEF-ADR-0047
  seccion 2). Este es el de **Comandos**; el otro proposito de este BC (Consultas, CQS) es un
  servidor y una key separados (`Bitakora.ControlAsistencia.Mcp.Consultas`).
- **Tools 100% stateless**: el contexto conversacional vive en el cliente MCP, nunca aqui.
- **Respuestas remodeladas para token-eficiencia**: cada tool poda campos internos y trunca
  listas largas con senal para que el asistente refine el filtro.

## Identidad y gate OAuth (MEF-ADR-0047 decisiones 6-7, MEF-ADR-0032 seccion 9)

- **Propagador de identidad, siempre activo**: cada HttpClient tipado hacia una Function App del
  BC inyecta `X-Tenant-Id`/`X-User-Id` via `PropagadorIdentidadTenantHandler`. El valor es
  interino por app settings (`Identidad__TenantIdInterino`/`Identidad__UserIdInterino`) mientras
  el servidor no reciba identidad real de una tool call -- ver el `// TODO` en
  `Infraestructura/ConfiguracionIdentidadTenant.cs`.
- **Limite estructural del host**: las tool calls contra `/runtime/webhooks/mcp` llegan a este
  worker **sin** header `Authorization` -- lo sirve el paquete del host de la extension MCP, que
  no lo reenvia. Ningun middleware del worker puede exigirlo. El gate OAuth real de este servidor
  vive exclusivamente en el borde (Azure API Management, variante MCP/Connect).
- **`AutorizacionMcpMiddleware`/`ValidadorTokenAuthKit`**: defensa en profundidad, `ValidateAudience
  = false` (la audiencia ya la exige la politica de APIM). Este BC ya esta en
  `tenancy.strategy = "multi-tenant-header"`, asi que `Program.cs` los cablea directo.
- **PRM (`MetadataRecursoProtegido/`)**: descubrimiento anonimo RFC 9728, servido en
  `/api/.well-known/oauth-protected-resource` (routePrefix por defecto); la ruta raiz que exige el
  RFC la publica el borde de APIM mapeando a esa. Responde `503` mientras `Mcp__ResourceUri`/
  `Mcp__AuthorizationServer` no sean URIs absolutas -- el Terraform del servidor siembra
  `Mcp__AuthorizationServer` con el dominio AuthKit real del entorno (el mismo que usa
  `Bitakora.ControlAsistencia.Mcp.Consultas`) y `Mcp__ResourceUri` con un `PENDIENTE-...` hasta que
  exista el modulo `apim-mcp-comandos` que provea la URL de APIM de este servidor.

## Estado de este scaffold

Generado por `/scaffold-mcp` (fase 1 + fase 2 + fase 3): proyecto del servidor, tool de ejemplo,
propagador de identidad y componentes OAuth app-side (seccion anterior),
endpoints de gate, unit tests base, Terraform (Service Plan + Storage + Function App), el workflow
de deploy encadenado tras el apply de infra, la suite **SmokeTests** con las cinco verificaciones
canonicas del nivel 3 de la piramide de testing (handshake, tools/list vivo, tool call de lectura,
error path del `.resx`, 401 sin key -- MEF-ADR-0048 secciones 1-2) y el reusable
`smoke-tests-mcp.yml` compartido con `Mcp.Consultas`, con su job `smoke-tests` encadenado tras el
deploy.

### SmokeTests

- Proyecto: `tests/Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests/`. Cliente MCP real
  (`ModelContextProtocol.Core`) contra el endpoint desplegado -- cero `ProjectReference` al BC.
- **Compila en el CI de PRs, pero no se ejecuta ahi**: esta en el `.slnx`, asi que cualquier
  `dotnet build` de la solucion la compila; los jobs de test iteran el glob
  `tests/Bitakora.ControlAsistencia.*.Tests/` y el sufijo `.SmokeTests` queda fuera -- igual que
  las suites `SmokeTests` de dominio. Corre contra el entorno desplegado en el job `smoke-tests`
  del workflow de deploy (o a mano, exportando las dos variables de abajo).
- Configuracion: `Mcp:BaseUrl`/`Mcp:FunctionsKey` por `appsettings.json` (BaseUrl real, key vacia),
  `appsettings.local.json` (ignorado por git) o las variables de entorno
  `Mcp__BaseUrl`/`Mcp__FunctionsKey`. La key nunca vive en un archivo versionado: en CI se lista en
  runtime con `az functionapp keys list` (MEF-ADR-0047 decision 5, MEF-ADR-0048 seccion 4).
- La tool de ejemplo (`ejemplo_listar`) consume el catalogo real de turnos de Programacion
  (`GET api/programacion/turnos`), no un endpoint ficticio: sus smoke tests ya ejercitan un camino
  vivo desde el primer deploy, aunque el catalogo este vacio en un tenant sin datos. Al reemplazar
  `ejemplo_listar` por las tools reales de Comandos, actualiza los asserts **pinneados** de
  `ComposicionDelHost/` y `Ejemplo/`: el catalogo exacto de `tools/list` y el error path del
  `.resx` son contrato, no muestreo (MEF-ADR-0048 seccion 2, verificaciones 2 y 4).

## Tools

| Tool | Que responde | Parametros |
|---|---|---|
| `ejemplo_listar` | **EJEMPLO** -- catalogo de turnos de Programacion: id, nombre | `filtro_nombre?` |

Reemplaza `ejemplo_listar` por las tools reales de Comandos (lenguaje ubicuo, MEF-ADR-0040) antes
de publicar este servidor.

## Onboarding de un cliente MCP (una vez desplegado)

### 1. Obtener la system key

La key `mcp_extension` la genera el host de Functions cuando el codigo ya esta desplegado
(MEF-ADR-0047 decision 5) -- **no se versiona ni se copia a configuracion commiteada**.

```bash
az functionapp keys list \
  -g <resource-group-del-entorno> \
  -n <nombre-de-la-function-app> \
  --query systemKeys.mcp_extension -o tsv
```

### 2. Conectar un cliente MCP

Endpoint fijo: `/runtime/webhooks/mcp` (transporte Streamable HTTP; SSE esta deprecado). La key
viaja en el header `x-functions-key` -- sin ella el host responde `401`.

```bash
claude mcp add --transport http comandos \
  https://<nombre-de-la-function-app>.azurewebsites.net/runtime/webhooks/mcp \
  --header "x-functions-key: <key del paso 1>"
```

### 3. Verificar

En una conversacion nueva: el servidor aparece conectado (`/mcp`) y lista las tools de la tabla
de arriba; una consulta real debe invocar `ejemplo_listar` y devolver datos del entorno.
