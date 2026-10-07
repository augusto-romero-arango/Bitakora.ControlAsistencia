# CA-ADR-0039: Doctrina de consultas -- endpoints de lista y tools MCP

## Estado

Propuesto (2026-10-05). Precisa y se desvia de **MEF-ADR-0042 seccion 2** (paginacion keyset: forma del sobre, cursor opaco, `Take` opcional) y precisa **MEF-ADR-0047 decision 4** (truncado con senal: modalidades, paginacion, sin total). Generaliza **CA-ADR-0038** (composicion interna sin `Take`) a todo endpoint de lista. Se propone al marco. Field note de origen: `docs/bitacora/field-notes/2026-10-05-1138-planner.md` (PR #875), refinamiento de #876.

## Contexto

Al refinar #876 (advertencias de la programacion semanal en el asistente) aparecio que cada tool de consulta del asistente y cada endpoint de lista resolvian "que pasa cuando hay mas" a su manera:

- **Tools**: ninguna paginaba. Topes de 20, 50 o ninguno (`listar_sedes`). `listar_colaboradores` y `buscar_colaboradores` pedian 200 al backend y reportaban "20 de 200" con 500 reales (**total falso**). Notas con guia concreta, generica o ninguna. Resultado vacio como frase en unas y lista vacia muda en otras. `consultar_ausencias` mandaba al modelo a encadenar `listar_colaboradores` para armar el equipo: con un tope intermedio, la consulta perdia personas **en silencio**.
- **Backend**: tres formas para colecciones parecidas -- devolver todo (`GET programacion/turnos`, `plantillas-semanales`, `sedes/fichas`, `colaboradores/etiquetas/categorias`), cota de rango sin paginar (`turnos-vigentes`, `asistencias-diarias`, `programacion/ausencias`) y keyset con `Take` (`colaboradores/fichas`, `directorio`, `resumenes-asistencia`). Respuestas como arreglo directo o como sobre (`filas`). Ningun endpoint devolvia el cursor siguiente: el cliente lo armaba con la ultima fila y conocia la clave de orden.

Referencias:
- Spec MCP 2025-06-18 y 2025-11-25, "Pagination": cursor **opaco**, el servidor decide el tamano de pagina, `nextCursor` ausente = fin, el cliente no interpreta el cursor. Aplica a `tools/list`, `resources/list` y similares, no a `tools/call`: la paginacion de resultados de una tool es diseno de cada servidor.
- Servidores MCP oficiales: Slack (`cursor` + `limit`, devuelve `next_cursor`), GitHub (`pkg/github/params.go`: `page`/`perPage`, `after`, y un contrato "unificado" entre backends REST y GraphQL; responde `pageInfo { hasNextPage, nextCursor }`), Notion (`start_cursor` + `page_size`, `has_more` + `next_cursor`). Ninguno devuelve total.
- Anthropic Engineering, "Writing effective tools for AI agents" (2025): combinar paginacion, seleccion de rango, filtrado y truncado con defaults sensatos; al truncar, guiar al agente con instrucciones accionables; devolver informacion de alta senal.
- Claude Code: advierte cuando una tool MCP supera 10.000 tokens y corta en 25.000 por defecto (`MAX_MCP_OUTPUT_TOKENS`). Si la tool no se acota, el cliente lo hace a ciegas.

## Decision

### Principio

1. **Una tool de consulta le responde al usuario, no a otra tool.** Si una tarea necesita una coleccion completa como insumo (el grupo de una sede para consultar sus ausencias), la composicion se hace **en codigo**, dentro de la tool, contra las APIs internas y sin `Take` (CA-ADR-0038). Nunca encadenando tools: un tope intermedio pierde informacion en silencio. Pasar el id de **un** elemento que el usuario eligio (`buscar_colaboradores` -> `solicitar_programacion_turno`) si esta permitido.

### Tools MCP: modalidades por intencion

2. Cada tool de consulta pertenece a una modalidad, definida por la intencion del usuario:

   | Modalidad | Intencion | Tools hoy | Cuando hay mas | Tope |
   |---|---|---|---|---|
   | **Detalle** | "Muestrame esto a fondo" | `obtener_turno`, `obtener_plantilla_semanal`, `listar_colaboradores` con `identificacion` | No aplica (una entidad; sus listas internas se muestran completas) | -- |
   | **Desambiguacion** | "A quien se refiere el usuario?" | `buscar_colaboradores` | **No pagina**: acotar o preguntar al usuario. Se buscan unos pocos; hay que ser certero | 20 |
   | **Catalogo** | "Ubicame un elemento / muestrame el catalogo" | `listar_turnos`, `listar_plantillas_semanales`, `listar_sedes`, `listar_colaboradores` por sede o etiquetas | **Pagina** con `cursor`; la nota sugiere primero refinar | 50 |
   | **Ventana temporal** | "Que pasa en este periodo?" | `consultar_programacion`, `consultar_ausencias`, #876 | **Pagina** con `cursor`; la nota sugiere primero acotar sujetos o ventana | 50 |

   "Excepciones" (solo lo anomalo) no es modalidad: es el filtro de relevancia de MEF-ADR-0047 que ya cumple la ventana temporal (solo aparece quien tiene algo que reportar).

3. **Tope fijo**, decidido por el servidor; el modelo no elige el tamano de pagina.
4. **Ventana temporal**: recibe la ventana y responde con la ventana **aplicada** (si el backend la recorto, la nota lo dice); sujetos por `sede`, `etiquetas` o `codigos_colaborador`, resueltos en codigo (decision 1). **La unidad de pagina la decide cada reporte** (colaborador, dia...) y se nombra en la lista y en la nota.
5. **Sobre de toda lista** (desambiguacion, catalogo, ventana temporal):
   - `mostrando`: siempre.
   - `siguienteCursor`: solo si hay mas y la modalidad pagina; es el `siguienteCursor` del backend reenviado tal cual. Ausente = fin.
   - `nota`: solo si hubo corte o recorte; dice que paso y la accion concreta (parametro para acotar primero, cursor despues; en desambiguacion, acotar o preguntar). Textos en `.resx` (MEF-ADR-0009).
   - La lista con el **nombre del recurso** (`turnos`, `colaboradores`, `dias`...), elementos remodelados con solo campos de alta senal (MEF-ADR-0047).
   - Las ventanas temporales agregan la ventana aplicada.
   - **Sin `total`.** Nunca un total, ni exacto ni estimado.
6. **Vacio**: una frase con el criterio usado ("Nadie falta entre ... y ... en la sede Norte"), nunca un sobre vacio mudo.
7. **Detalle** sin sobre: la entidad; si no existe, una frase con el criterio (el mensaje del dominio).

### Backend: endpoints de lista

8. **Toda lista pagina por keyset**, incluidos los catalogos chicos. `GET` lleva `take` y `cursor` como parametros planos de query (MEF-ADR-0042 seccion 1); QUERY, en el body.
9. **`Take` opcional.** Sin `Take`: todo lo que cumple el filtro (dentro de la cota de rango si la hay) -- lo usa la composicion interna (generaliza CA-ADR-0038). Con `Take`: una pagina acotada a **200** (MEF-ADR-0042 seccion 2). Esto alinea `directorio`, que hoy respeta `Take` sin maximo (CA-ADR-0038 decision 2).
10. **Sobre unico en el body**: `{ "elementos": [ ... ], "siguienteCursor": "..." }`. `siguienteCursor` es **opaco** (p. ej. campos keyset serializados en base64url), solo viaja si hay mas; el endpoint pide `Take + 1` para saberlo. El cliente lo reenvia tal cual en `cursor` y nunca lo arma ni lo interpreta. La lista se llama `elementos` en todos los endpoints (un solo helper de paginacion en los clientes).
11. **Ventanas temporales**: mantienen su cota de rango (acota la ventana, no la pagina) y el sobre agrega la ventana aplicada (`desde`, `hasta`, `rangoRecortado`).
12. **Sin `total`.** Si una pantalla necesita un conteo, se disena como dato propio de la vista, no como efecto colateral del listado.
13. **Vacio**: `200` con `elementos: []`, nunca `404`. **Detalle** (`GET .../{id}`): la entidad o `404`.

### Conexion entre capas

14. La tool pide al backend `Take = tope + 1`; si llega el extra, hay mas. Su cursor es el `siguienteCursor` del backend; no arma cursores propios. La composicion interna llama sin `Take`. La tool traduce `elementos` al nombre del recurso al remodelar.

## Consecuencias

- **Una sola mecanica** de paginacion para el front, las tools y la composicion interna; cambiar la clave de orden de un endpoint no rompe clientes.
- **Ninguna perdida silenciosa**: el modelo siempre sabe si vio todo (sin `siguienteCursor`) o si hay mas (con cursor y nota accionable).
- **Se pierde el total** que hoy dan `listar_turnos`, `listar_plantillas_semanales`, `consultar_programacion` y `consultar_ausencias`. Un conteo se disena explicitamente.
- **Ruptura del front** en los endpoints que hoy responden arreglo directo o sobre `filas`, y en los cursores armados por el cliente: migracion explicita con inventario y aviso a consumidores, mismo regimen que MEF-ADR-0043 seccion 7.
- **Trabajo de alineacion**: 12 endpoints de lista y 7 tools de lista; las ventanas temporales ganan `sede` y `etiquetas`; la descripcion de `consultar_ausencias` deja de mandar a encadenar `listar_colaboradores`.
- **Revision**: cuando el marco resuelva la propuesta, este CA-ADR pasa a superado o se alinea.

## Alternativas descartadas

- **Exponer el tamano de pagina al modelo** (`limit`/`perPage`, como Slack, GitHub y Notion): en ellos refleja la API envuelta; aqui agrega una decision que el modelo puede tomar mal.
- **Total exacto cuando es barato**: comportamiento diferenciado; contar exige cargar todo o una segunda consulta.
- **Catalogo y desambiguacion sin paginar**: el catalogo completo es una pregunta legitima ("que turnos tenemos?"). La desambiguacion si se mantiene sin paginar.
- **Modalidad "conjunto" que pagina para alimentar otras tools**: contradice el principio de la decision 1.
- **Unidad de pagina impuesta (siempre colaborador)**: cada reporte decide si lo importante es la persona o la fecha.
- **Cursor en encabezado `Link` (RFC 8288)**: raro con QUERY, que lleva el cursor en el body; el sobre ya era la forma de las ventanas.
- **Lista con nombre del recurso en el backend**: el backend es API para codigo; el nombre del recurso es para el modelo, en la tool.
- **Paginar por numero de pagina**: el backend es keyset; un offset obliga a recorrer desde el principio.
