# Servidor MCP de consultas — ControlAsistencias

Servidor [MCP](https://modelcontextprotocol.io/) remoto del bounded context, desplegado como
Azure Functions con la extension `Microsoft.Azure.Functions.Worker.Extensions.Mcp`. Expone tools
de **solo lectura** sobre los endpoints HTTP ya desplegados de los dominios; es un cliente HTTP
puro, sin referencias a los ensamblados del BC (issue #502).

## Proposito y limites

- **Un servidor MCP por Bounded Context y por proposito**, nunca por dominio. Este es el de
  **Consultas** del par Consultas/Comandos (CQS): la separacion lectura/escritura vive en la
  frontera del servidor y su key, no en hints del protocolo. El de Comandos es un incremento
  futuro y tendra su propio Function App y su propia key.
- **Tools 100% stateless**: la "ventana de trabajo" (que fechas, que colaborador se esta
  discutiendo) vive en la conversacion del cliente, nunca en el servidor — es serverless:
  scale-to-zero y multiples instancias hacen inviable cualquier estado por sesion.
- **Respuestas remodeladas para token-eficiencia**: cada tool poda campos internos (stream keys,
  centinelas), compacta estructuras profundas (`"06:00-10:00, descanso 12:00-13:00, sede: Norte"`)
  y trunca listas largas con senal para que el asistente refine el filtro.

## Tools

| Tool | Que responde | Parametros |
|---|---|---|
| `listar_turnos` | Catalogo de turnos activos: id, nombre, horario | `filtro_nombre?` |
| `obtener_turno` | Detalle de un turno: franjas, descansos, extras, sede prearmada | `id` |
| `listar_sedes` | Sedes activas: codigo, nombre, ciudad, direccion | `filtro_nombre?` |
| `consultar_programacion` | Que turno rige a cada colaborador en un rango de fechas | `desde`, `hasta`, `codigo_colaborador?`, `sede_id?` |

## Onboarding de un cliente MCP (entorno dev)

Requisitos: Azure CLI autenticado con acceso de lectura a la suscripcion de dev, y el codigo ya
desplegado en el Function App (workflow `deploy-mcp-consultas.yml`).

### 1. Obtener la system key

El host de Functions genera la key `mcp_extension` cuando el codigo con la extension MCP ya esta
desplegado (si el comando devuelve vacio, el deploy no ha corrido). La key **no se versiona ni se
copia a configuracion commiteada**: se obtiene por CLI en cada onboarding.

```bash
az functionapp keys list \
  -g rg-controlasistencias-dev \
  -n func-asist-dev-mcp-consultas \
  --query systemKeys.mcp_extension -o tsv
```

### 2. Conectar Claude Code

El endpoint del host es fijo: `/runtime/webhooks/mcp` (transporte Streamable HTTP; SSE esta
deprecado). La key viaja en el header `x-functions-key` — sin ella el host responde 401.

```bash
claude mcp add --transport http consultas-asistencia \
  https://func-asist-dev-mcp-consultas.azurewebsites.net/runtime/webhooks/mcp \
  --header "x-functions-key: <key del paso 1>"
```

### 3. Verificar

En una conversacion nueva de Claude Code:

1. El servidor `consultas-asistencia` aparece conectado (`/mcp`) y lista las 4 tools de la tabla.
2. Una consulta real responde con datos de dev — por ejemplo: *"lista los turnos"* debe invocar
   `listar_turnos` y devolver el catalogo, y *"que programacion hay entre el 1 y el 31 de julio
   de 2026"* debe invocar `consultar_programacion` con ese rango.

## Verificacion del despliegue

Este proyecto no tiene smoke tests ni endpoint `/api/version` (excepcion deliberada a
MEF-ADR-0013 registrada en el issue #509: el reusable de smoke tests exige `/api/version`, que el
host MCP no expone). La verificacion es el onboarding manual de arriba; si el piloto se consolida,
un issue futuro agrega SmokeTests + `/api/version`.
