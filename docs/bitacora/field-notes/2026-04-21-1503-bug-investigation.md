---
fecha: 2026-04-21
hora: 15:03
sesion: bug-investigator
tema: Mensajes acumulados en subscription control-horas-escucha-programacion vs historial "verde" de smoke tests
---

## Sintoma reportado

Primer reporte: tras smoke tests de ControlHoras, quedaron mensajes acumulados en la subscription
`control-horas-escucha-programacion` del topico `programacion-turno-diario-solicitada` que no fueron
consumidos. Los SequenceNumber 398 y 399 se procesaron HOY (2026-04-21) tras el deploy con
`DeliveryCount=1`.

Segundo reporte (que genero la contradiccion a resolver): el usuario afirma que el historial de los
smoke tests "dice que estaban en verde". Esto entra en conflicto con la hipotesis H1 previa (Function
Apps dormidas en plan B1 con `alwaysOn=false` durante 4 dias) porque un smoke test dependiente de
consumo real por la Function App no podria haber pasado si la app estaba dormida sin despertar.

## Investigacion

### 1. Historial real de smoke tests en GitHub Actions

Query: `gh api repos/.../actions/runs?per_page=100&created=>2026-04-13`

Resultados clave (ordenados):

| Fecha (UTC) | Workflow | Resultado | Trigger |
|---|---|---|---|
| 2026-04-10 01:30 | Smoke Tests | success | workflow_dispatch |
| 2026-04-10 17:39 | Smoke Tests | success | workflow_dispatch |
| 2026-04-13 14:50 | Smoke Tests | **failure** | workflow_dispatch (error en `cat` / jq) |
| 2026-04-13 14:53 | Smoke Tests | success | workflow_dispatch |
| 2026-04-13 23:27 | Smoke Tests | success | workflow_dispatch |
| **Gap: 14-abr al 21-abr** | — | — | Sin ejecuciones de smoke |
| 2026-04-21 17:01 | Deploy ControlHoras | success | push (merge PR #111) |

**Conclusion**: no hubo NINGUNA ejecucion de smoke tests en GitHub Actions entre el 14-abr y el 21-abr.
El "verde" que reporta el usuario corresponde al **ultimo run exitoso** (13-abr 23:27), no a ejecuciones
diarias pasando en verde. El historial muestra "verde" porque la ultima vez que se ejecuto paso, pero
no se volvio a ejecutar.

Ambos workflows de smoke (`smoke-tests.yml` y `smoke-tests-dominio.yml`) tienen solo triggers
`workflow_dispatch` / `workflow_call` — no corren en push ni en PR. Deben dispararse manualmente.

### 2. Detalle del ultimo run exitoso de ControlHoras (13-abr 23:27)

Log del job `smoke-test (ControlHoras, ...)`:

- Total: 3 tests (Health + `DebeAsignarTurnoDiario_CuandoSeBusPublicaProgramacionTurnoDiarioSolicitada` +
  `DebeAsignarTurnoDiario_CuandoMensajeTieneFormatoCamelCaseDeWolverine`).
- Succeeded: 3, Failed: 0, Skipped: 0.
- Duration: **5s 854ms** (muy rapido, consistente con Function App caliente o cold start subsegundo).
- `ServiceBus__ConnectionString` y `Postgres__ConnectionString` fueron provistos como secrets.

El `dotnet test` NO fue saltado: el output dice "total: 3, succeeded: 3, skipped: 0". Si hubiera habido
`Assert.SkipWhen(!serviceBus.IsConfigured)` disparandose, `skipped` seria > 0.

En el mismo run tambien corrio Programacion en verde. Ese smoke **no publica al topico destino de
ControlHoras**, publica a endpoints HTTP propios del dominio Programacion (otra ruta).

Ambos runs del 13-abr duraron ~5.7s. El del 14:53 ocurrio apenas 3 minutos despues del fallo 14:50, asi
que la app podria haber estado parcialmente despierta. El de 23:27 ocurrio 8h mas tarde, con la app
presumiblemente dormida. Un cold start de Azure Functions B1 Basic para isolated worker .NET 10 puede
tomar entre 2-5s, asi que 5.7s para 3 tests (1 health + 2 con polling) es apretado pero viable si el
evento se procesa apenas la app despierta.

### 3. Que publico los 2 mensajes (SequenceNumber 398 y 399) acumulados

Evidencia que descarta al workflow de GitHub Actions:
- No hay runs de smoke en GitHub entre 14-abr y 21-abr.

Evidencia que apunta a **smoke tests locales del desarrollador**:
- `git reflog` y `git log --since=2026-04-20` muestran actividad intensa del usuario durante 19, 20 y 21
  de abril en HU-105 (RegistrarMarcacion de entrada o salida).
- PR #111 mergeado hoy (21-abr) a las 12:01 COT (17:01 UTC), que coincide exactamente con el
  `Deploy ControlHoras` y con el consumo tardio de SequenceNumber 398 y 399 con `DeliveryCount=1`.
- `AsignarTurnoViaSbSmokeTests` lee credenciales desde `appsettings.local.json` o variables
  `ServiceBus__ConnectionString` / `Postgres__ConnectionString` — si el dev ejecuta el proyecto de
  smoke localmente con esos secrets puestos, los mensajes se publican al topico **productivo** (dev env).

Contradiccion adicional dentro del propio test: `AsignarTurnoViaSbSmokeTests` usa `Guid.CreateVersion7()`
para `empleadoId` y `solicitudId`. Si el dev ejecuto el test localmente mientras la app ControlHoras
estaba dormida, las credenciales son las mismas de dev (ServiceBus productivo), el mensaje se publica,
pero:

- Si ControlHoras nunca desperto durante los 30s del `Timeout`, el polling sobre Postgres retorna
  `false` y el assert `.Should().BeTrue(...)` falla -> el test localmente hubiera mostrado ROJO.
- Pero si la app SI desperto y consumio durante otra ventana, el test hubiera pasado y no quedarian
  mensajes acumulados.

El hecho de que queden 2 mensajes acumulados sugiere que el desarrollador ejecuto el test
localmente, la publicacion funciono, pero **la Function App no consumio dentro del timeout del polling**.
Sin embargo, el test habria fallado localmente. Esto solo es consistente si:

(a) El desarrollador cancelo la ejecucion del test antes de que termine (Ctrl+C durante el polling),
    dejando el mensaje encolado sin que la app lo consumiera a tiempo.
(b) El desarrollador ejecuto un publicador ad-hoc (no el test completo), por ejemplo probando el topico
    con un script o con la UI de Azure.
(c) El test esta marcado con `[Fact(Skip=...)]` u otro mecanismo que publica pero no verifica — **no
    es el caso**: revisado el codigo, el polling y assert estan presentes.

### 4. Revision del assert del smoke test (linea 60-72 de `AsignarTurnoViaSbSmokeTests.cs`)

El assert SI depende de que la Function App consuma:

1. Publica al topico productivo con GUIDs frescos.
2. Polling sobre `mt_events` en schema `control_horas`, buscando `turno_diario_asignado` para
   `streamId = {empleadoId}:{fecha}` con campo JSON `SolicitudId` igual al valor publicado.
3. Polling tiene timeout de 30s con backoff.
4. `WaitUntilTrueAsync` retorna `false` si se agota el timeout sin lanzar excepcion.
5. `.Should().BeTrue()` falla si el polling retorna false.

No hay falso positivo obvio: el streamId es unico por ejecucion (GUIDs v7), asi que no puede haber un
evento preexistente que haga pasar el polling sin que la Function App haya consumido.

Unica via de falso positivo teorico: si entre el momento en que se publica el evento y el momento en
que se consulta, **alguna OTRA Function App escribe al mismo `streamId`** en el schema control_horas.
Pero el streamId incluye el empleadoId (GUID v7), asi que es practicamente imposible.

Conclusion: el test es solido. Si paso verde el 13-abr, fue porque la Function App si consumio y
escribio en Postgres en ese momento.

### 5. Timeline de mensajes encolados (hipotesis)

Asumiendo que SequenceNumber 398 y 399 corresponden a publicaciones recientes (no del 13-abr):

- Entre 14 y 20 de abril el dev no publico nada al topico (mas probable — se enfoco en HU-105 que
  usa HTTP endpoint, no este topico).
- El 21-abr, al preparar/validar el cierre del PR #111, el dev probo el flujo SB de ControlHoras
  (quizas ejecutando localmente `AsignarTurnoViaSbSmokeTests` o publicando al topico con un script
  ad-hoc) con la app dormida, dejando 2 mensajes encolados.
- A las 17:01 UTC se desplego ControlHoras (PR #111 merge), la app arranca, se conecta al SB y
  consume los 2 mensajes pendientes con `DeliveryCount=1`.

**Alternativa (menos probable) que descarta H1**: otro servicio o job publica al topico periodicamente.
No encuentro evidencia de eso en el codigo: el unico publicador conocido del topico
`programacion-turno-diario-solicitada` es el dominio Programacion, que es tambien una Function App en
B1 con `alwaysOn=false`. Si Programacion publica, necesita haber sido despertada, y no veo un timer
trigger en ese codigo.

### 6. Otros periodos donde la app estuvo activa pero no consumio

No tengo evidencia. La ausencia total de traces en App Insights en los ultimos 7 dias (salvo deploy de
hoy) impide verificar esto. El script `appinsights-query.sh` silencia tablas vacias, pero ni una sola
consulta (`exceptions`, `function-errors`, `traces --filter programacion-turno-diario`) retorna datos
observables. Esto es **tambien consistente con H1** (apps dormidas = sin telemetry) pero no con
"smoke tests verdes regularmente durante el gap".

## Diagnostico

### La contradiccion es aparente, no real

El usuario interpreta "historial en verde" como "los smoke tests pasaron repetidamente durante el gap
de 4 dias". La evidencia muestra lo contrario:

- GitHub Actions no ejecuto NINGUN smoke test entre 14-abr y 21-abr.
- El ultimo run exitoso (marcando el historial en verde) es del 13-abr 23:27 UTC.
- El icono verde en el listado de workflows de GitHub refleja el **estado del ultimo run**, no la
  salud continua.

**Ambos hechos son consistentes y compatibles entre si:**

1. El smoke del 13-abr 23:27 paso verde porque la Function App consumio en ese momento (app estaba
   caliente o cold start fue rapido). Duration de 5.7s compatible.
2. Desde el 14-abr hasta el 21-abr 17:01 UTC, nadie ejecuto smoke tests en CI. La app se fue a dormir
   y no hubo mensajes publicados contra el topico en ese periodo (consistente con ausencia de
   telemetry).
3. El 21-abr, alguien (probablemente el propio desarrollador en local, durante cierre de HU-105)
   publico 2 mensajes al topico. Estos no se consumieron inmediatamente (app dormida + test local
   cancelado, o publicacion via script sin consumidor activo).
4. El deploy de ControlHoras a las 17:01 UTC de hoy disparo el arranque de la app, que proceso los
   2 mensajes acumulados con `DeliveryCount=1`.

### H1 (apps dormidas en el gap) sigue siendo consistente con los datos

H1 no entra en conflicto con el historial "verde" porque el verde no corresponde al gap, sino al
momento previo al gap. La acumulacion de mensajes es compatible con:

- Publicador local del dev (no CI), o
- Publicacion via herramienta externa (script, Azure Portal, explorer de SB),

combinado con la app dormida sin desencadenante automatico de despertar.

### Confianza

- **Alta**: la ausencia de runs de smoke en GitHub entre 14-abr y 21-abr es un hecho verificable.
- **Alta**: el ultimo run exitoso fue 13-abr 23:27 y paso con evidencia de polling exitoso contra
  Postgres.
- **Media**: la atribucion de los 2 mensajes encolados a publicacion local del dev es la hipotesis
  mas plausible pero no confirmada por logs directos (appinsights-query.sh no retorna filas para
  queries custom, y no tengo visibilidad de ejecuciones locales).
- **Baja-Media**: la explicacion del cold start de 5.7s para 3 tests; podria investigarse con un run
  controlado y medir tiempos exactos, pero no es critico para la conclusion.

## Acciones

Sin creacion de issues solicitada todavia. Recomendaciones al usuario:

1. **Validar con el dev** si efectivamente ejecuto smoke tests o alguna publicacion ad-hoc al topico
   `programacion-turno-diario-solicitada` durante hoy 21-abr antes del deploy. Esto confirma la
   hipotesis de publicacion local.
2. **Considerar automatizar smoke tests** tras cada deploy exitoso (`workflow_run` trigger en
   `smoke-tests.yml` que se dispare cuando `Deploy ControlHoras` o `Deploy Programacion` pase). Esto
   eliminaria ambiguedad sobre cuando corrieron por ultima vez y daria una senal clara de salud
   post-deploy.
3. **No cambiar conclusiones del bug original**: H1 (apps dormidas) sigue siendo la causa raiz
   probable de la acumulacion. No hay contradiccion real.

## Preguntas abiertas

- ¿Quien exactamente publico los 2 mensajes (SequenceNumber 398 y 399)? El usuario puede confirmarlo
  revisando su historial local (shell history, IDE, script runs).
- ¿Por que el smoke del 13-abr paso en solo 5.7s si la app deberia haber estado dormida? Hipotesis:
  deploy reciente (17:01 UTC de algun dia previo?) dejo la app caliente, o cold start mas rapido de
  lo esperado en B1. Podria validarse disparando un smoke manual ahora y midiendo el tiempo real.
- ¿Hay algun timer trigger en Programacion que publique al topico periodicamente? (Revisado
  superficialmente: no, pero vale la pena confirmar con `grep TimerTrigger src/Bitakora.ControlAsistencia.Programacion/`).
- ¿El script `appinsights-query.sh` oculta tablas vacias o hay un bug que silencia el output? El
  ultimo caso seria un bug de tooling separado a reportar.
