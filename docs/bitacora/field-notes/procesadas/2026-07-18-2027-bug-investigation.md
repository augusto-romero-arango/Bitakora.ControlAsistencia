---
fecha: 2026-07-18
hora: 20:27
sesion: bug-investigator
tema: Smoke tests siguen fallando en ambos dominios DESPUES del fix ITenantResolver (#219/#220) - race de deploy + DLQ residual + bug latente de validador
---

## Sintoma reportado

Tras mergear y desplegar el fix del `ITenantResolver` mono-tenant (`TenantResolverFijo`,
`AddScoped<ITenantResolver, TenantResolverFijo>()`, issue #219 / PR #220, en `origin/main`
commits `ecf9564` + merge `1cccfb3`), los smoke tests volvieron a fallar en AMBOS dominios:

- **Deploy Programacion** run `29667647602`: `build-and-test` OK, `deploy` OK, `smoke-tests` FAIL (10/11).
- **Deploy ControlHoras** run `29667647650`: `build-and-test` OK, `deploy` OK, `smoke-tests` FAIL (1/11).

La CI (build+test) del PR #220 paso en verde; el host arranca y responde al warmup, pero los smoke
fallan. El reporter pidio distinguir: (a) fix no llego a runtime, (b) fix resolvio el 500 pero hay
un fallo distinto, (c) fallo de datos/estado/entorno del smoke, (d) cadena Service Bus rota.

## Investigacion

### 1. Logs del step "Smoke tests" (asserts concretos)

**Programacion (run 29667647602, smoke 00:54:18-00:55:01Z): 10 de 11 fallan, TODOS HTTP 500.**
- `CrearTurno_DebeRetornar202_CuandoPayloadEsValido`: esperaba 202, recibio 500.
- `CrearTurno_DebeRetornar400_CuandoPayloadEsInvalido`: esperaba 400, recibio 500.
- `CrearTurno_DebeRetornar409_CuandoTurnoYaExiste`: esperaba 409, recibio 500.
- `SolicitarProgramacionTurno_DebeRetornar400_CuandoFechasEstaVacia`: esperaba 400, recibio 500.
- `DebePublicarProgramacionTurnoDiarioSolicitada_CuandoSolicitudEsAceptada`: 500. Etc.
- Hasta los casos de validacion (400) devuelven 500 -> fallo de activacion/DI antes de validar
  (misma firma que ANTES del fix).

**ControlHoras (run 29667647650, smoke 00:54:18-00:57:48Z): 1 de 11 falla, 10 pasan.**
- Los HTTP (RegistrarMarcacion, Health) PASAN -> los 500 desaparecieron.
- El unico fallo es la cadena SB: `AsignarTurnoViaSbSmokeTests.DebeAsignarTurnoDiario_CuandoSeBus...`
  (duro 22.4s, fallo en **linea 117**): `Expected collection to be empty ... dead letter de
  'control-horas-escucha-programacion' ... but found at least one item {MessageId:498044ab...}`.
- Que fallara en la linea 117 (assert de DLQ) implica que PASO todas las aserciones previas: el
  `turno_diario_asignado` SI se persistio (linea 78), `InformacionEmpleado` y `DetalleTurno`
  coincidieron, y `DiaCalculado` SI se publico (lineas 103-114). Es decir, la ruta SB del private
  event router **proceso el evento del test end-to-end sobre codigo nuevo**. Solo fallo por hallar
  un dead letter residual (498044) dejado por el warmup.
- El warmup (issue#166, corre ANTES de toda la suite) fallo aparte: `TimeoutException: El listener
  AsignarTurno no persistio turno_diario_asignado ... dentro de 120s` (logueado 00:57:45Z).

### 2. App Insights (controlasistencias-dev-ai / rg-controlasistencias-dev, via `az monitor app-insights query`)

Excepciones agrupadas por firma desde 00:30:00Z:

| firma | count | primera | ultima |
|---|---|---|---|
| ITenantResolver (Command + PrivateEvent router) | 89 | 00:34:42 | **00:54:58** |
| RpcException | 11 | 00:54:51 | 01:20:14 (mis probes) |
| System.NullReferenceException (validador) | 2 | 01:20:14 | 01:20:14 (mi probe) |
| System.ArgumentOutOfRangeException | 2 | 01:20:16 | 01:20:16 (mi probe) |
| TimeoutException / NpgsqlException (Postgres transitorio) | 3 | 00:56:25 | 00:56:25 |

Detalle de las excepciones de `ITenantResolver` (durante la ventana del smoke, 00:54:49-00:54:58):
- rol vacio (= Programacion, no exporta cloud_RoleName): `Unable to resolve service for type
  'Cosmos.MultiTenancy.ITenantResolver' while attempting to activate '...WolverineCommandRouter'`
  (24 IOE / ruta HTTP).
- rol `func-asist-dev-control-horas`: `Unable to resolve ... ITenantResolver ... while attempting to
  activate '...WolverinePrivateEventRouter'` (10 IOE / ruta Service Bus, coincide con las 10 entregas
  antes de dead-letter).

**Clave: las excepciones de ITenantResolver arrancan 00:34:42 y CESAN a las 00:54:58. Cero despues.**
El deploy termino 00:54:13-14; el paquete nuevo (`WEBSITE_RUN_FROM_PACKAGE`) quedo vivo ~00:55 y
desde entonces no hay ni una sola excepcion de ITenantResolver. El fix ES efectivo en runtime.

### 3. Probe en vivo del estado ACTUAL (>30 min post-deploy, no destructivo)

- `GET  /api/health` (Programacion) -> 200
- `POST /api/programacion/turnos` `{}` (CrearTurno) -> **400** (el command router YA se activa: fix vivo)
- `POST /api/programacion/solicitudes` `{}` (SolicitarProgramacionTurno) -> **500** (fallo distinto, ver abajo)
- `GET  /api/health` (ControlHoras) -> 200
- `POST /api/control-horas/marcaciones` `{}` (RegistrarMarcacion) -> 202 (HTTP ok; ver observacion menor)

La excepcion actual de `/solicitudes` (capturada por el probe, App Insights 01:20:14): NO es
ITenantResolver, es `System.NullReferenceException: NullReferenceException occurred when executing
rule for x => x.Empleado.EmpleadoId`. Bug del validador (ver correlacion).

### 4. Correlacion con el codigo (origin/main)

- `Program.cs` de AMBOS dominios registra `AddScoped<ITenantResolver, TenantResolverFijo>()` antes de
  `AgregarWolverineCommandRouter()` (y ControlHoras ademas `AgregarWolverinePrivateEventRouter()`).
  Cableado del command router identico en ambos. El fix esta bien escrito.
- `AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction/FunctionEndpoint.cs`: el consumidor SB
  inyecta `IPrivateEventRouter` por constructor y se activa por el DI del worker de Functions (mismo
  contenedor que los HTTP). Por eso, con codigo nuevo, resuelve `ITenantResolver` igual que el command
  router (confirmado: el evento del propio test SB se proceso end-to-end).
- `SolicitarProgramacionTurnoFunction/CommandHandler/SolicitarProgramacionTurnoValidator.cs` **linea 13**:
  `RuleFor(x => x.Empleado.EmpleadoId).NotEmpty();` sin guarda de nulos. Si `Empleado` es null (body
  malformado), FluentValidation desreferencia y lanza NRE -> 500 en vez de 400.
- Smoke `SolicitarProgramacionTurno_DebeRetornar400_CuandoFechasEstaVacia` (linea 264) envia `empleado`
  POBLADO con `fechas` vacio -> el validador evalua bien y devuelve 400. El bug del validador NO lo
  ejercita el smoke; sus 500 en el run fueron el race, no el NRE.
- Timeline del smoke ControlHoras: deploy fin 00:54:13; warmup publica el evento SB temprano
  (~00:54:40, durante el swap) -> codigo viejo procesa -> ITenantResolver -> dead-letter 00:54:50-52.
  El warmup luego hace polling 120s (falla 00:57:45). Los tests HTTP y el test SB corren DESPUES
  (~00:57:45+, ya con codigo nuevo) y el evento propio del test SB fluye OK; solo el assert de DLQ
  vacio cae por el residuo del warmup.
- DLQ actual de `control-horas-escucha-programacion`: **29 dead letters** acumulados (runs previos +
  el race), 0 activos. El assert `deadLetters.Should().BeEmpty()` seguira cayendo mientras existan.

## Diagnostico

**Causa raiz del fallo de smoke en AMBOS dominios (confianza ALTA): race entre el deploy y el smoke.**
El fix de #219/#220 es correcto y efectivo; simplemente NO estaba vivo en runtime cuando corrio el
smoke. El step "Deploy to Azure Functions" reporta exito al subir el paquete + sync-triggers, pero el
swap/reinicio de `WEBSITE_RUN_FROM_PACKAGE` sigue en curso. El smoke arranca ~4s despues (deploy fin
00:54:13/14 -> smoke inicio 00:54:18). El health-warmup puede responder 200 sobre la instancia vieja,
y el listener SB puede procesar sobre codigo viejo, ANTES de que el paquete nuevo quede montado.

Evidencia que lo prueba:
- Programacion `CrearTurno`: 500 en el smoke (00:54) -> **400 ahora** (recuperado). Toda la app estaba
  en codigo viejo durante el smoke.
- Excepciones de ITenantResolver confinadas a 00:34-00:54:58; **cero despues** de que el paquete nuevo
  quedo vivo (~00:55).
- ControlHoras: HTTP paso (corrio tarde, codigo nuevo) y el evento propio del test SB se proceso
  end-to-end (turno persistido + DiaCalculado publicado) = ruta SB OK con codigo nuevo. El unico
  fallo fue el dead letter residual del warmup (que corrio temprano, codigo viejo).

Un unico mecanismo (race de swap) explica los dos sintomas, con dos agravantes independientes:

**Agravante A - test SB no resiliente a DLQ residual (confianza ALTA).**
`AsignarTurnoViaSbSmokeTests` purga la subscription `smoke-tests` de `dia-calculado` antes del Act
(linea 65) pero NO purga el DLQ de entrada `control-horas-escucha-programacion`. Con 29 dead letters
acumulados, el assert `deadLetters.Should().BeEmpty()` (linea 120) falla aunque el consumidor funcione
perfecto. Ademas el warmup, al fallar por el race, deja un dead letter que envenena la corrida.

**Agravante B - bug latente del validador de Programacion (confianza ALTA, no rompe el smoke).**
`SolicitarProgramacionTurnoValidator` desreferencia `x.Empleado.EmpleadoId` sin guarda de nulos ->
NRE -> 500 en vez de 400 ante body con `Empleado` null. No lo ejercita el smoke (siempre manda
empleado poblado), pero es un fallo real de robustez/contrato. Confirmado en vivo (`{}` -> 500).

### Hipotesis descartadas con evidencia

- **(a) "El fix no llego a runtime" (como causa persistente): DESCARTADA.** Programacion `CrearTurno`
  ahora devuelve 400 y las excepciones de ITenantResolver cesaron 00:54:58. El fix SI llego; solo
  llego tarde para el smoke (race). Fue transitorio, no un artefacto obsoleto permanente.
- **(b) "El fix resolvio HTTP pero la ruta SB quedo rota (gap de wiring del private event router)":
  DESCARTADA.** El evento propio del test SB fluyo end-to-end sobre codigo nuevo (turno persistido +
  DiaCalculado publicado); el FunctionEndpoint se activa por el DI del worker igual que los HTTP. La
  falla SB del 00:54:50 fue codigo viejo (race), no un gap de registro.
- **(d) "Cadena Service Bus / topics-subscriptions rota": DESCARTADA.** Topic y subscription existen y
  funcionan; el consumidor procesa correctamente con codigo nuevo. Lo unico "roto" es el residuo en el
  DLQ y el assert que no lo tolera.
- **(c) "Fallo de datos/estado del smoke": PARCIAL.** Aplica solo como el DLQ residual (Agravante A),
  no como causa de los 500 de Programacion.
- **Noisy-neighbor por plan compartido (#43): NO aplica.** Los fallos son excepciones deterministas
  (DI / NRE), no timeouts intermitentes sin excepcion.

Observaciones menores:
- ControlHoras `RegistrarMarcacion` con `{}` devolvio 202 y luego lanzo `ArgumentOutOfRangeException`
  async (mi probe con basura). Posible gap de validacion sincrona del body de marcacion. Baja prioridad,
  a verificar.
- 2-3 excepciones transitorias de Postgres (`Exception while reading from stream`) a las 00:56:25,
  probablemente del agente de durabilidad de Wolverine (Solo). Sin impacto en el smoke; monitorear.

## Acciones propuestas (sin ejecutar; requieren confirmacion; el bug-investigator no modifica codigo)

1. **Readiness gate deploy->smoke (tipo:tooling)** — issue: el smoke arranca antes de que el paquete
   nuevo quede vivo. Anadir una compuerta que confirme codigo NUEVO antes de correr smoke: exponer el
   commit/build SHA en `/api/health` (o un `/api/version`) y hacer que el warmup haga polling hasta que
   reporte el SHA desplegado; o reiniciar la Function App tras el deploy y esperar readiness real.
   Labels sugeridos: `bug, tipo:tooling, dom:controlhoras, dom:programacion, estado:listo`.
   (Aplica a `deploy-*.yml` / `smoke-tests-dominio.yml`, compartidos por ambos dominios.)

2. **Test SB resiliente a DLQ residual (tipo:refactor, dom:controlhoras)** — issue: `AsignarTurnoViaSb
   SmokeTests` debe purgar el DLQ de `control-horas-escucha-programacion` antes del Act (como ya hace
   con `dia-calculado/smoke-tests`), o acotar el assert al MessageId/SolicitudId de la corrida (fallar
   solo si un dead letter corresponde a ESTE evento). Revisar tambien que el warmup no deje residuo que
   envenene la suite. Labels: `bug, tipo:refactor, dom:controlhoras, estado:listo`.

3. **Guarda de nulos en SolicitarProgramacionTurnoValidator (tipo:refactor, dom:programacion)** — issue:
   `RuleFor(x => x.Empleado).NotNull()` con cascade, o `When(x => x.Empleado != null)` en las reglas
   anidadas / validador hijo, para devolver 400 (no 500) ante body con `Empleado` null. Labels:
   `bug, tipo:refactor, dom:programacion, estado:listo`.

Workarounds inmediatos (NO ejecutados; requieren permiso explicito):
- Purgar los 29 dead letters de `control-horas-escucha-programacion/$DeadLetterQueue` para que el
  proximo smoke de ControlHoras no caiga por residuo (necesario ademas de la Accion 1/2).
- Re-lanzar los workflows de deploy ahora que las apps estan en codigo nuevo:
  - Programacion: deberia quedar VERDE (fix vivo, bug de validador no ejercitado por el smoke).
  - ControlHoras: seguira ROJO en el test SB hasta purgar el DLQ o aplicar la Accion 2, aunque el
    consumidor ya funcione.

## Preguntas abiertas

- Por que el swap de Programacion fue mas lento que el de ControlHoras (toda la app vieja durante el
  smoke vs solo la ventana SB temprana)? Variabilidad del restart o del mount del paquete; medir con
  el version-endpoint de la Accion 1.
- El `ArgumentOutOfRangeException` de `RegistrarMarcacion` ante body malformado: hay un gap de
  validacion sincrona del payload de marcacion (retorna 202 y falla async)? Verificar; posible issue
  aparte de robustez.
- Confirmar si prod corre estas mismas versiones/pipeline; si aplica, el race y el bug del validador
  valen tambien alli.
- Las excepciones transitorias de Postgres del agente de durabilidad: vigilar si escalan.
