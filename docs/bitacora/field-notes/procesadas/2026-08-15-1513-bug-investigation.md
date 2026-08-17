---
fecha: 2026-08-15
hora: 15:13
sesion: bug-investigator
tema: HTTP 500 en RegistrarColaborador tras deploy (run 31904647437) — timeout de Npgsql durante migracion de esquema de Marten en cold start
---

## Sintoma reportado

El workflow "Deploy Colaboradores" run 31904647437 (merge del PR #388, issue #376 "Adoptar verbos
canonicos en los endpoints de etiquetas", commit 25cad4ac) fallo en el job `smoke-tests`.

Fallo 1 test: `ObtenerFichaColaborador_Retorna200ConLaMismaFicha_CuandoElIdDeRutaViajaEnMinusculas`.
El fallo fue en el **arrange**: la llamada a `RegistrarColaborador` devolvio HTTP 500 en vez de 202,
tras 1m02s de espera. Ventana: 2026-08-15 entre 19:44 y 19:49 UTC.

Los smoke tests locales contra el mismo dev pasan 103/103.

## Investigacion

### Obstaculo de tooling encontrado primero (bloqueante)

`scripts/appinsights-query.sh` devolvio **vacio** para `exceptions`, `function-errors` y
`health-summary` — un falso "todo OK". Causa: `az monitor app-insights query --output table` no
renderiza nada en `azure-cli 2.82.0` + extension `application-insights 1.2.3`. Verificado con el
caso trivial:

```bash
az monitor app-insights query --app controlasistencias-dev-ai -g rg-controlasistencias-dev \
  --analytics-query "print x=1" -o table   # -> vacio
az monitor app-insights query ... -o json  # -> {"tables":[{... "rows":[[1]]}]}
```

Toda la investigacion se rehizo con `-o json` + formateador propio. Sin esto, la sesion habria
concluido "no hay evidencia en App Insights".

### Recoleccion (queries KQL, todas con `-o json`)

1. Volumen de telemetria 4h por `itemType` -> si habia datos (137 excepciones en el bin 19:30-20:00).
2. `requests | summarize by cloud_RoleName, name, resultCode` (4h) -> **exactamente 1 request 500**.
3. Detalle de la request 500 -> `operation_Id`, instancia, duracion.
4. `exceptions | where operation_Id == '...'` con `details` -> stack trace completo.
5. Historico 14d de 500s y de timeouts -> evento unico.
6. `traces` en la ventana 19:40-19:50 -> reconstruccion de la linea de tiempo por `cloud_RoleInstance`.
7. `dependencies` en la ventana -> duracion de la invocacion y de las dependencias postgresql.
8. Ventana del deploy siguiente (19:55-20:10) -> el *near miss*.

Metricas de Azure Monitor (fuera de App Insights): `psql-asist-dev` (cpu_percent, memory_percent,
active_connections, connections_failed, connections_succeeded) y `asp-asist-dev-colaboradores`
(CpuPercentage, MemoryPercentage), intervalo PT1M.

### La request 500

| Campo | Valor |
|---|---|
| timestamp | 2026-08-15T19:45:39.6590067Z |
| name | `POST api/colaboradores` |
| duration | **35261.96 ms (35.3 s)** |
| resultCode | 500 |
| operation_Id | `bc65ea2c90831abcf8af8c81c4735bae` |
| cloud_RoleInstance | `786e5bc5230b` |

### La excepcion

`System.TimeoutException: "The operation has timed out."` (Npgsql 9.0.4.0), envuelta por
`Microsoft.Azure.WebJobs.Script.Workers.Rpc.RpcException` a las 19:46:14.95 -> HTTP 500.

Cadena del stack (de adentro hacia afuera):

```
Npgsql.TaskTimeoutAndCancellation.ExecuteAsync
  <- Npgsql.Internal.NpgsqlConnector.ConnectAsync
  <- Npgsql.Internal.NpgsqlConnector.RawOpen
  <- Npgsql.PoolingDataSource.OpenNewConnector
  <- Npgsql.NpgsqlConnection.OpenAsync
  <- Weasel.Core.Migrations.DatabaseBase.executeMigration        (DatabaseBase.cs:516/528)
  <- Weasel.Core.Migrations.DatabaseBase.generateOrUpdateFeature (DatabaseBase.cs:507)
  <- Weasel.Core.Migrations.DatabaseBase.ensureStorageExistsAsync(DatabaseBase.cs:471/468)
  <- Marten.Events.QueryEventStore.FetchStreamStateAsync         (QueryEventStore.cs:224)
  <- Cosmos.EventSourcing.CritterStack.TaskAggregateRootExtensions.Map
  <- RegistrarColaboradorCommandHandler.HandleAsync             (linea 28)
  <- Wolverine Executor.InvokeAsync / InvokeInlineAsync
  <- RegistrarColaboradorFunction.FunctionEndpoint.Run          (linea 34)
```

El timeout NO ocurre ejecutando una consulta de negocio: ocurre **abriendo la conexion** que Weasel
necesita para correr la **migracion de esquema** de Marten (`AutoCreate = CreateOrUpdate`, el default,
confirmado en los comentarios de `ComposicionServicios.cs`), disparada por `ensureStorageExistsAsync`
en la primera operacion contra el event store tras el arranque del worker.

Correlacion con el codigo (ambas lineas coinciden exactamente):
- `RegistrarColaboradorCommandHandler.cs:28` = `await _eventStore.ExistsAsync<ColaboradorAggregateRoot>(streamId, ct)` -> `FetchStreamStateAsync`.
- `FunctionEndpoint.cs:34` = `await commandRouter.InvokeAsync(comando!, ct)`.

### Linea de tiempo (traces por `cloud_RoleInstance`)

| Hora UTC | Instancia | Evento |
|---|---|---|
| 19:44:37 - 19:45:23 | `b5bfc2585ca2` (vieja) | 21x `GET /api/version` cada ~2.1s — el gate del workflow esperando el SHA |
| 19:45:26.22 | **`786e5bc5230b` (nueva)** | primera aparicion: responde `/api/version` con el SHA nuevo -> **gate abierto** |
| 19:45:38.85 | nueva | `GET /api/health` -> 200 |
| 19:45:39.52 | nueva | `GET /api/colaboradores/fichas/...` |
| **19:45:39.688** | nueva | **`POST /api/Colaboradores`** — primera escritura contra el proceso recien arrancado |
| 19:46:13.83 - 19:46:39.13 | nueva | 13 `TimeoutException` de Npgsql, en pares, reintentos cada ~5s |
| 19:46:14.95 | nueva | `RpcException` -> HTTP 500 -> el smoke test falla |
| 19:46:41.81 | nueva | siguiente `POST /api/Colaboradores` -> **202**; a partir de aqui todo normal |

Ventana de indisponibilidad del write-path: ~19:45:26 -> ~19:46:40 (**~74 s**).

### El servidor PostgreSQL estaba sano

`psql-asist-dev` (Standard_B1ms, Burstable, PG 17), ventana 19:35-20:00, intervalo 1m:

- `cpu_percent`: 8.0 - 11.3 % (sin pico alguno; 8.85 % a las 19:45, 8.22 % a las 19:46)
- `memory_percent`: ~58-61 % estable
- `active_connections`: 14-19 (limite de B1ms ~35)
- **`connections_failed`: 0.0 en TODOS los minutos de la ventana**
- `connections_succeeded`: 1.0 constante

El servidor no rechazo ninguna conexion ni estaba saturado. Descarta agotamiento de `max_connections`
y descarta throttling de Postgres.

### El cuello de botella estaba en el lado del Function App

`asp-asist-dev-colaboradores` (**B1 Basic, 1 core, capacity 1, plan dedicado** — MEF-ADR-0020 OK,
cada dominio tiene su propio plan):

| Minuto UTC | CpuPercentage | MemoryPercentage |
|---|---|---|
| 19:44 | 13 % | 76 % |
| **19:45** | **62 %** | 81 % |
| **19:46** | **80 %** | 81 % |
| 19:47 | 60 % | 84 % |
| 19:48 | 92 % | 91 % |

Durante 19:45:26 - 19:45:23 convivieron **dos instancias** (`b5bfc2585ca2` saliendo y `786e5bc5230b`
arrancando) sobre **un solo core**, mientras el worker nuevo hacia JIT + codegen de Wolverine +
descubrimiento/migracion de esquema de Marten.

Config del Function App: `alwaysOn: false`, `healthCheckPath: null`, `numberOfWorkers: 1`,
`preWarmedInstanceCount: 0`.

### El deploy siguiente reprodujo el patron (near miss)

Run 31905426735 (PR #397, **exitoso**): instancia nueva `ea7384eb4056` aparece a las 20:03 y su
primera escritura tardo **24191.18 ms (24.2 s)** — devolvio 202, pero se quedo justo por debajo del
umbral. Mismo patron, distinto lado del filo.

Comparativa de duraciones de `POST api/colaboradores` (4h):

| Poblacion | n | valor |
|---|---|---|
| 500 (la fallida) | 1 | 35261.96 ms |
| 202 exitosas — p50 | 299 | **111.65 ms** |
| 202 exitosas — max (primera tras cold start del deploy siguiente) | 1 | 24191.18 ms |

Ratio: la request fallida tardo **~316x** el p50 normal.

### Descarte del bug de logica

`git show --stat 25cad4ac` — el commit toca **unicamente**:
`AsignarEtiquetaFunction/` (4 archivos), `RetirarEtiquetaFunction/` (3), smoke tests de etiquetas y
fichas, tests unitarios de endpoints y `IdentificacionTests.cs`.

**No toca `RegistrarColaboradorFunction/`** ni el handler ni `ComposicionServicios.cs`. Ninguno de
los archivos del stack trace fue modificado por el PR.

### Alcance temporal

- `requests` del rol `func-asist-dev-colaboradores`, ultimos 14 dias, agrupado por dia:
  **1427 requests, 1 sola con resultCode 500** (la investigada).
- `exceptions` de tipo timeout en 14 dias: **13, todas en la hora 19:00 del 2026-08-15**, todas en la
  instancia `786e5bc5230b`.
- App Insights: retencion 90 dias, sampling 100 %, daily cap 0.5 GB (reset 21:00).
  Nota: la telemetria de este rol solo aparece a partir de hoy — verificar si es por antiguedad del
  dominio o por perdida de ingesta.

### Terreno previo relacionado

- **#119** "Endurecer smoke tests ante cold start de Function Apps" (cerrado, `dom:control-horas`).
- **#166** "Calentar la cadena Service Bus con un warm-up funcional en el fixture de smoke tests"
  (cerrado, `bug`, `tipo:tooling`) — establece el precedente del **warm-up funcional**, aplicado al
  listener de Service Bus de ControlHoras.
- **#224** "Condicionar el smoke al SHA desplegado via endpoint /api/version" (cerrado) — introdujo
  el gate que aqui se muestra insuficiente para el write-path.

Ninguno cubre el write-path HTTP + Marten de Colaboradores: los fixtures de
`Bitakora.ControlAsistencia.Colaboradores.SmokeTests/Fixtures/` tienen `Polling` (esperas de
resultado) pero **ningun warm-up funcional** del event store.

## Diagnostico

**Causa raiz (confianza alta, evidencia convergente): timeout de conexion de Npgsql durante la
migracion de esquema de Marten (`ensureStorageExistsAsync`), disparada por la primera operacion
contra el event store en un worker recien arrancado, bajo saturacion de CPU del plan B1 de 1 core.**

No es un bug de logica del PR #388: el commit no toca ninguno de los archivos del stack.
No es un fallo de PostgreSQL: el servidor estaba al 8 % de CPU con `connections_failed = 0`.
No es noisy neighbor por plan compartido (MEF-ADR-0020): el plan es dedicado, `numberOfSites` = 1.

El mecanismo: con `alwaysOn = false` y `AutoCreate = CreateOrUpdate`, cada deploy produce un worker
frio que paga el descubrimiento/migracion de esquema de Marten **en la primera request de negocio**,
no en el arranque. Esa primera request compite por el unico core con el JIT, el codegen de Wolverine
y la instancia saliente, y el handshake de conexion de Npgsql vence su timeout antes de completarse.

**Fallo secundario que lo convierte en fallo de CI**: el gate de readiness del workflow
(`smoke-tests-dominio.yml`) abre en cuanto `/api/version` reporta el SHA, y `/api/health` devuelve
`OkObjectResult("OK")` **sin tocar Postgres ni Marten**. Ambos endpoints confirman "el binario nuevo
esta cargado", ninguno confirma "el event store esta listo". Los smoke tests entran ~13 s antes de
que el write-path pueda responder.

## Acciones

Ninguna ejecutada. **No se crearon issues** (pendiente de confirmacion del usuario). Propuestas:

1. **`bug, tipo:tooling, dom:tooling`** — Reparar `scripts/appinsights-query.sh`: cambiar
   `--output table` por `-o json` con formateo propio. Hoy todas las queries predefinidas devuelven
   vacio silenciosamente con az-cli 2.82.0 / app-insights 1.2.3, produciendo falsos "sin hallazgos".
   Es el hallazgo de mayor impacto transversal: envenena cualquier investigacion futura.
2. **`bug, tipo:tooling, dom:colaboradores`** — Warm-up funcional del write-path en el gate de
   readiness o en el fixture de smoke (precedente #166): tras confirmar el SHA en `/api/version`,
   ejercitar una operacion que toque el event store y esperar a que responda, antes de soltar la
   suite. Alternativa complementaria: que `/api/health` verifique realmente la conexion a Marten.
3. **`tipo:infra`** — Evaluar `always_on = true` en `asp-asist-dev-colaboradores` (y/o
   `ApplyAllDatabaseChangesOnStartup` para mover la migracion al arranque). Ambas opciones tienen
   coste y fueron descartadas antes por precio (#166 lo documenta); decision del usuario.

## Preguntas abiertas

- **Timeout efectivo de Npgsql**: el default es 15 s y la request duro 35.3 s (≈2 intentos + retry de
  Weasel). No se inspecciono el connection string (esta en Key Vault, CA-ADR-0026) para confirmar si
  `Timeout`/`Command Timeout` estan personalizados. Verificar antes de proponer ajustes de valores.
- **Por que los timeouts vienen en pares** (19:46:13.83/13.84, 14.58/14.62, 18.45/18.46, 23.98/23.98,
  28.44/28.45): sugiere dos rutas de migracion concurrentes o dos schemas. No confirmado.
- **Telemetria de solo un dia** para este rol con retencion de 90 d y sampling 100 %: verificar si es
  por antiguedad del dominio o por agotamiento del daily cap de 0.5 GB (CA-ADR-0009).
- **Recurrencia**: 1 fallo de 2 deploys consecutivos, con el segundo a 24.2 s de un umbral de ~35 s.
  Sin cambio, la probabilidad de repeticion por deploy es alta. Monitorear `maxDur` de la primera
  escritura tras cada deploy como indicador adelantado.
- ¿Conviene un ADR sobre readiness real del write-path tras deploy (vs. readiness de binario), dado
  que ya hay tres issues cerrados orbitando el mismo tema (#119, #166, #224)?
