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
- `registrar_sede` consume el Function App de Sedes (`POST api/sedes`, #456) y `registrar_colaborador`
  el de Colaboradores (`POST api/colaboradores`, #574): sus asserts **pinneados** de
  `ComposicionDelHost/` y de la carpeta propia de cada tool son contrato, no muestreo (MEF-ADR-0048
  seccion 2, verificaciones 2 y 4) -- toda tool nueva actualiza el catalogo exacto de `tools/list`
  y su error path del `.resx`.

## Tools

| Tool | Que registra | Parametros |
|---|---|---|
| `registrar_sede` | Sede nueva de la empresa (`POST api/sedes`) | `codigo`, `nombre`, `ciudad?`, `direccion?` |
| `registrar_colaborador` | Colaborador nuevo bajo control de asistencia, con su vinculacion desde `fecha_inicio` (`POST api/colaboradores`) | `tipo_identificacion`, `numero_identificacion`, `primer_nombre`, `segundo_nombre?`, `primer_apellido`, `segundo_apellido?`, `codigo_colaborador`, `fecha_inicio`, `codigo_sede?` |
| `solicitar_programacion_turno` | Un turno programado a cada colaborador de la lista, solo los dias de la ventana que su vinculacion cubre (`POST api/programacion/solicitudes`, una solicitud por colaborador) | `desde`, `hasta`, `turno`, `sede_de_programacion`, `identificaciones` |

`solicitar_programacion_turno` es la unica tool consolidada del servidor (MEF-ADR-0047 decision 4):
resuelve el turno contra `GET api/programacion/turnos`, la sede contra `GET api/sedes/fichas/{codigo}`
y re-verifica a los colaboradores contra `QUERY api/colaboradores/directorio` antes de emitir N
comandos. La **sede de programacion** -- donde queda registrada la programacion -- **no es la sede de
trabajo** del colaborador: la tool la exige explicitamente y nunca la asume. Un lote no es concepto
del dominio (la ventana de trabajo es efimera, nunca se persiste) y el marco no ofrece atomicidad
entre streams: el fallo de un colaborador no detiene a los demas, viaja en `fallidos[]`.

## Limitacion conocida: `resource` ausente en la tool call (#571)

El descubrimiento OAuth de este servidor y el de `Mcp.Consultas` comparten el mismo Authorization
Server (AuthKit). Si el cliente MCP omite el parametro `resource` al pedir el token -- en vez de
usar el PRM de **este** servidor (`Mcp.Comandos`) -- AuthKit puede emitir un token con `aud` de
Consultas. `AutorizacionMcpMiddleware`/`ValidadorTokenAuthKit` de Comandos rechazan ese token: la
audiencia no calza con la de este recurso. No hay workaround del lado del servidor; el cliente
debe declarar `resource` apuntando al PRM de Comandos en cada tool call.

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
de arriba; un pedido real debe invocar `registrar_sede` y devolver el eco de la sede registrada.
