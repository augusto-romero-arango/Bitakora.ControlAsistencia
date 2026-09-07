# CA-ADR-0035: Codigos de exito e idempotencia de los comandos HTTP

## Estado

Aceptado (sesion de planeacion 2026-09-05; aplica desde #620-#623; correccion de los endpoints
existentes en #640). Precisa MEF-ADR-0004 ("Respuestas HTTP" y seccion 2) -- propuesto al marco en
harness#849 (codigos de exito) y harness#850 (idempotencia).

## Contexto

Los 29 endpoints HTTP de comando del BC respondian `202 Accepted`, siguiendo MEF-ADR-0004 (*"202
Accepted -- comando aceptado, efectos downstream son asincronos"*; *"el HTTP siempre responde 202 si
paso la validacion"*) y MEF-ADR-0043 (*"el marco ya tenia POST 202 Accepted como respuesta estandar
de comando"*). Al refinar #620 el experto pregunto si el POST "realmente es un Accepted o el turno si
queda creado en la transaccion". Verificado descompilando `Cosmos.EventSourcing.CritterStack 2.3.1`:

- `WolverineCommandRouter.InvokeAsync` -> `IMessageBus.InvokeAsync(command)`: ejecucion inline del
  handler, no un enqueue.
- `AgregarWolverineParaComandosServerless` registra `UnitOfWorkMiddleware` (su `After()` hace
  `Append` de los eventos no confirmados a la `IDocumentSession`) y `Policies.AutoApplyTransactions()`:
  el middleware transaccional Wolverine+Marten ejecuta `SaveChangesAsync()` al terminar el handler,
  antes de que `InvokeAsync` retorne. Ningun handler del dominio llama `SaveChangesAsync`.
- Los smoke tests ya lo corroboraban sin decirlo: `POST` + `POST` inmediato -> 409; `POST` + `DELETE`
  inmediato -> exito.

RFC 9110 seccion 15.3.3 define 202 como *"the request has been accepted for processing, but the
processing has not been completed"*. Aqui el procesamiento termino: el stream existe cuando el cliente
recibe la respuesta. Lo asincrono es el read-side (proyecciones `Async`, MEF-ADR-0034) y los efectos
que cruzan el bus -- que no son el recurso pedido. El 202 mentia a todo consumidor externo.

Segundo vacio, descubierto al refinar #622: MEF-ADR-0004 seccion 2 fija la idempotencia **por
trigger** (crear si ya existe: HTTP 409 / bus silencio) pero no dice nada de "quitar lo que ya no
esta" ni "poner lo que ya esta". El repo acumulo cuatro respuestas distintas: `AsignarSede` con la
misma sede -> exito sin evento (`SinCambios`); `RetirarEtiqueta` sobre categoria vacia -> 409 ("el
typo debe aflorar", #355); `RetirarCentroDeCostos` sin CC -> 409; `RetirarDispositivo` no instalado
-> 404; `RetirarTurno` repetido -> 409.

## Decision

### 1. El codigo de exito lo fija el commit, no el trigger

Criterio decidible: al retornar la respuesta, ¿el recurso ya quedo confirmado en el event store?

- **Si** -- el caso de todo `CommandHandler` bajo `AutoApplyTransactions` --: codigo sincronico
  segun el paso del test de precedencia de MEF-ADR-0043 seccion 2:

| Paso MEF-ADR-0043 | Verbo / forma | Codigo de exito |
|---|---|---|
| 1 -- crea una entidad | `POST {coleccion}` | `201 Created` + `Location` con la URI canonica de lectura (si existe GET canonico; si no, 201 sin `Location`) |
| 2 -- reemplaza un VO direccionable | `PUT {recurso}/{sub}` | `204 No Content` (el slot existe por construccion); `201` solo si el PUT crea una representacion que antes no existia |
| 3 -- remueve un sub-recurso | `DELETE ...` | `204 No Content` |
| 4 -- accion de negocio | `POST {recurso}:{verbo}` | `204 No Content` (sin body) o `200 OK` si devuelve representacion |

- **No** -- el handler no persiste nada del recurso y solo publica/encola un mensaje cuyo efecto
  ocurre despues --: `202 Accepted`, con la justificacion en el issue. Regla del experto, textual:
  *"Accepted solo lo podemos usar cuando cerramos la peticion y lo que emitimos fue un mensaje.
  Created si persistimos el objeto en el mismo POST y ya esta disponible para su uso."* Hoy ningun
  endpoint del BC cumple la condicion del 202: los tres que publican al bus
  (`SolicitarProgramacionTurno`, `CancelarProgramacion`, `RegistrarMarcacion`) persisten primero su
  propio aggregate.

El `Location` de un 201 apunta a la URI canonica del recurso aunque su read model se materialice con
retardo: la eventualidad del read-side se documenta (las tools MCP ya llevan "nota de visibilidad
eventual"), no se disfraza con un 202 en el write-side.

### 2. Un comando idempotente sobre un estado ya alcanzado responde exito sin evento

| Situacion al llegar el comando | Respuesta | Evento |
|---|---|---|
| El estado pretendido **ya esta alcanzado** (PUT con el mismo valor; DELETE de lo ya ausente; retirar lo ya retirado) | codigo de exito del contrato (204), **sin evento** -- el aggregate declina con `SinCambios` y el handler no lo traduce a excepcion | ninguno |
| El comando **conflictua** con el estado (asignar sobre una plantilla retirada; semana fuera de rango; PUT que viola una restriccion del recurso) | 409 (CA-ADR-0030) | ninguno |
| El **recurso direccionable no existe** (stream inexistente; sub-recurso por id que nunca existio) | 404 | ninguno |
| **Crear** un stream que ya existe (`POST`) | 409, como MEF-ADR-0004 -- POST no es idempotente y el payload puede diferir | ninguno |

Fundamento: RFC 9110 seccion 9.2.2 (idempotente = el efecto pretendido de N requests identicas es el
de una), secciones 9.3.4/9.3.5 (PUT/DELETE son idempotentes) y 15.5.10 (409 = conflicto con el
estado actual; un slot vacio no conflictua con "dejalo vacio"). El argumento "el typo debe aflorar"
es preferencia de UX contra el estandar; el estado queda observable en el read-side.

### 3. Regimen de migracion

Espejo de MEF-ADR-0043 seccion 7: la doctrina aplica a los endpoints nuevos desde #620-#623 (201 en
`POST programacion/plantillas-semanales`, 204 en sus PUT/DELETE, no-ops idempotentes). Los 29
endpoints existentes se corrigen en #640 (inventario completo, consumidores que pinean 202, y los
cuatro DELETE que responden 409/404 a un no-op), con aviso a consumidores integrados. `RetirarDispositivo`
se revisa caso a caso: si el `{dispositivoId}` nunca existio, el 404 es correcto.

Los agentes del pipeline (test-writer, implementer, reviewer) reciben la regla via el issue hasta que
harness#849/#850 la lleven al marco: el issue declara el codigo de exito junto al contrato HTTP y el
comportamiento no-op de todo PUT/DELETE.

## Alternativas consideradas

- **Mantener 202 por coherencia** con los endpoints existentes: descartada -- perpetua una imprecision
  que confunde commit con efectos downstream y hace que un consumidor reintente o haga polling sin
  necesidad.
- **201 con el id en el body**: innecesario -- el cliente genera el id (MEF-ADR-0037); `Location`
  basta (RFC 9110 seccion 9.3.3). Revisable por endpoint.
- **409 en el no-op ("el typo debe aflorar")**: descartada -- contradice la idempotencia que el propio
  paso 2/3 de MEF-ADR-0043 invoca para justificar el verbo; genera ruido al cliente.
- **Excepciones tipadas `RecursoYaExisteException`/`RecursoNoEncontradoException`** (enmienda
  2026-09-01 de MEF-ADR-0004): el BC no las ha scaffoldeado; los issues de esta sesion siguen el
  patron vigente (`InvalidOperationException` -> 409, `KeyNotFoundException` -> 404). Migrarlas es
  otro issue.

## Consecuencias

### Positivas
- La API deja de mentir: 201/204 dicen "ya esta"; 202 queda reservado a lo que de verdad es asincrono.
- Un DELETE repetido o un PUT sin cambios no obliga al cliente a distinguir "error" de "ya estaba".
- El criterio es decidible desde el issue: el planner fija codigo y no-op junto al contrato HTTP.

### Negativas
- Codigos mixtos en el BC hasta que cierre #640 (29 endpoints + sus tests unitarios, smoke y fakes MCP).
- Un `Location` puede apuntar a un GET que responde 404 durante la ventana de materializacion.
- La doctrina vive aqui hasta que el marco la incorpore; mientras, los agentes solo la reciben por el
  issue.

## Referencias

- RFC 9110 (HTTP Semantics): secciones 9.2.2, 9.3.3, 9.3.4, 9.3.5, 15.3.2, 15.3.3, 15.5.10.
- `Cosmos.EventSourcing.CritterStack 2.3.1`: `WolverineExtensions.AgregarWolverineParaComandosServerless`,
  `UnitOfWorkMiddleware`, `MartenEventStore`, `WolverineCommandRouter` (descompilados 2026-09-05).
- MEF-ADR-0004 (precisado), MEF-ADR-0043 (paso de precedencia -> codigo), MEF-ADR-0011 (contrato HTTP
  del DoR), MEF-ADR-0034, MEF-ADR-0037, MEF-ADR-0047; CA-ADR-0030, CA-ADR-0034.
- Issues: #620-#623 (primeros endpoints conformes), #640 (inventario y correccion), harness#849,
  harness#850.

## Control de cambios

- 2026-09-05: creado (sesion planner; refinamiento de #620-#629).
