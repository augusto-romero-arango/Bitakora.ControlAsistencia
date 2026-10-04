# Servidor MCP de Asistente -- ControlAsistencias

Servidor [MCP](https://modelcontextprotocol.io/) remoto del bounded context, desplegado como
Azure Functions con la extension `Microsoft.Azure.Functions.Worker.Extensions.Mcp`
(MEF-ADR-0047). Cliente HTTP puro de las Function Apps del BC -- cero `ProjectReference` hacia
ningun proyecto del BC.

## Proposito y limites

- **Un servidor MCP por Bounded Context y por proposito**, nunca por dominio (MEF-ADR-0047
  seccion 2). Este es el de **Asistente**; si el BC necesita la particion Consultas/Comandos
  (CQS), el otro proposito es un servidor y una key separados.
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
  = false` (la audiencia ya la exige la politica de APIM). Se generan siempre; si al scaffoldear
  este servidor `tenancy.strategy` ya era `multi-tenant-header`, `Program.cs` los cablea. Si no,
  quedan como propuesta comentada en `Program.cs` -- corre `/install-auth` y cablealos a mano (o
  vuelve a scaffoldear).
- **PRM (`MetadataRecursoProtegido/`)**: descubrimiento anonimo RFC 9728, servido en
  `/api/.well-known/oauth-protected-resource` (routePrefix por defecto). Esa ruta nunca es la URL
  publica del PRM: la fija el gateway APIM (output `prm_url` del modulo `apim-mcp-api`, forma
  `https://<apim>/well-known/oauth-protected-resource/<path-del-servidor>`, sin punto inicial por
  restriccion de APIM), que reescribe hacia esta Function con `<rewrite-uri>`. Los clientes MCP
  descubren esa URL siempre por el `resource_metadata` del `WWW-Authenticate` del `401` (RFC 9728
  seccion 5.1, MEF-ADR-0032 seccion 9), nunca por convencion well-known. Responde `503` mientras
  `Mcp__ResourceUri`/`Mcp__AuthorizationServer` no sean URIs absolutas -- el Terraform del servidor
  los siembra con un `PENDIENTE-...` hasta que el modulo `apim-mcp-api` del gateway los resuelve.

## Estado de este scaffold

Generado por `/scaffold-mcp` (fase 1 + fase 2 + fase 3): proyecto del servidor, tool de ejemplo,
propagador de identidad, componentes OAuth app-side (seccion anterior), el middleware que restaura
los argumentos `string` coercionados a fecha/GUID por la extension MCP
(`Infraestructura/ArgumentosCrudosMcpMiddleware.cs`, siempre activo -- temporal mientras upstream
no preserve el texto original, `Azure/azure-functions-mcp-extension#129`),
endpoints de gate, unit tests base, Terraform (Service Plan + Storage + Function App), el workflow
de deploy encadenado tras el apply de infra, la suite **SmokeTests** con las cinco verificaciones
canonicas del nivel 3 de la piramide de testing (handshake, tools/list vivo, tool call de lectura,
error path del `.resx`, 401 sin key -- MEF-ADR-0048 secciones 1-2) y el reusable
`smoke-tests-mcp.yml` con su job `smoke-tests` encadenado tras el deploy. El camino valido de todo
parametro fecha o identificador de una tool tiene su propia tool call en el smoke (MEF-ADR-0048
seccion 2 verificacion 3) -- `ejemplo_listar` lo demuestra con `fecha_referencia`.

### SmokeTests

- Proyecto: `tests/Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests/`. Cliente MCP real
  (`ModelContextProtocol.Core`) contra el endpoint desplegado -- cero `ProjectReference` al BC.
- **Compila en el CI de PRs, pero no se ejecuta ahi**: esta en el `.slnx`, asi que cualquier
  `dotnet build` de la solucion la compila; los jobs de test iteran el glob
  `tests/Bitakora.ControlAsistencia.*.Tests/` y el sufijo `.SmokeTests` queda fuera -- igual que las suites
  `SmokeTests` de dominio. Corre contra el entorno desplegado en el job `smoke-tests` del workflow
  de deploy (o a mano, exportando las dos variables de abajo).
- Configuracion: `Mcp:BaseUrl`/`Mcp:FunctionsKey` por `appsettings.json` (BaseUrl real, key vacia),
  `appsettings.local.json` (ignorado por git) o las variables de entorno
  `Mcp__BaseUrl`/`Mcp__FunctionsKey`. La key nunca vive en un archivo versionado: en CI se lista en
  runtime con `az functionapp keys list` (MEF-ADR-0047 decision 5, MEF-ADR-0048 seccion 4).
- Al reemplazar `ejemplo_listar` por las tools reales del BC, actualiza los asserts **pinneados**
  de `ComposicionDelHost/` y `Ejemplo/`: el catalogo exacto de `tools/list` y el error path del
  `.resx` son contrato, no muestreo (MEF-ADR-0048 seccion 2, verificaciones 2 y 4). Toda tool nueva
  o modificada con un parametro fecha o identificador gana su propia tool call de camino valido en
  el smoke (MEF-ADR-0048 seccion 2 verificacion 3, seccion 6) -- omitir el parametro o solo probar
  el error path no cubre esa restauracion.

## Tools

| Tool | Que responde | Parametros |
|---|---|---|
| `ejemplo_listar` | **EJEMPLO** -- catalogo de Programacion: id, nombre | `filtro_nombre?`, `fecha_referencia?` |

Reemplaza `ejemplo_listar` por las tools reales de tu BC (lenguaje ubicuo, MEF-ADR-0040) antes
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

Registra el servidor `asistente` en tu cliente MCP con transporte HTTP:

```text
url:    https://<nombre-de-la-function-app>.azurewebsites.net/runtime/webhooks/mcp
header: x-functions-key: <key del paso 1>
```

### 3. Verificar

En una conversacion nueva: el servidor aparece conectado (`/mcp`) y lista las tools de la tabla
de arriba; una consulta real debe invocar `ejemplo_listar` y devolver datos del entorno.

## Onboarding de un cliente MCP (`{gateway}/mcp-asistente`)

Este servidor expone las 27 tools de lectura y escritura del BC y **reemplaza a los conectores
de Consultas y Comandos**: desconectalos y conecta uno solo.

- **claude.ai**: Configuracion -> Conectores -> Agregar conector personalizado; URL
  `{gateway}/mcp-asistente`. Completa el login OAuth (AuthKit) y elige la empresa.
- **VS Code**: en `.vscode/mcp.json` agrega
  `{"servers": {"asistente": {"type": "http", "url": "{gateway}/mcp-asistente"}}}` e inicia el
  servidor; VS Code abre el flujo OAuth al primer uso.

Para cambiar de empresa, usa `cerrar_sesion` y reconecta.
