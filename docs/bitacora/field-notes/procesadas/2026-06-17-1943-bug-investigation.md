---
fecha: 2026-06-17
hora: 19:43
sesion: bug-investigator
tema: Smoke tests ControlHoras fallan en rerun (Cluster A turno_diario_asignado no escrito; Cluster B HTTP timeout 100s)
---

## Sintoma reportado
Rerun de smoke tests (ventana ~23:08-23:18 UTC del 2026-06-17), 5 fallos en 2 clusters:
- Cluster A: `turno_diario_asignado` nunca se escribe en el stream (3 tests). Uno de los mensajes
  de asercion sugiere que `ServiceBusDeserializador` no usa `PropertyNameCaseInsensitive=true`.
- Cluster B: endpoint HTTP `RegistrarMarcacion` no responde, `TaskCanceledException` por
  `HttpClient.Timeout` de 100s (2 tests).
Dos hipotesis previas ya refutadas: NO es Postgres saturado, NO es cold start (rerun con app caliente).

## Investigacion

### Correlacion con codigo (pista del deserializador) - REFUTADA
- `ServiceBusDeserializador.cs` SI usa `PropertyNameCaseInsensitive = true` (linea 14). El mensaje
  de asercion del test es solo un recordatorio historico del bug #29, no refleja el codigo actual.
- git log del deserializador: ultimo cambio `5d098fc` (HU-29), MUY anterior a #165. No es regresion.
- `ServiceBusEndpointBase.ProcesarMensaje`: ante cualquier excepcion hace `DeadLetterMessageAsync`.
  Si el deserializador o el handler fallaran, habria DLQ. NO la hay (ver abajo).

### Cambio de #165 (issue #131)
- Solo toco `AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaCommandHandler.cs`: agrega
  `await _publicEventSender.PublishAsync(control.CrearDiaCalculado())` tras Apply.
- `CrearDiaCalculado()` no lanza (usa `DesgloseHoras.Vacio`). Replica el patron de #108.
- Transaccion: Wolverine `AutoApplyTransactions()` + `IntegrateWithWolverine()` (outbox Marten).
  El `StartStream` y el mensaje saliente se commitean atomicamente al final del handler.

### App Insights (queries ejecutadas)
- `exceptions --hours 6`: CERO excepciones.
- `function-errors --hours 6`: CERO requests fallidas.
- `servicebus-dlq`: CERO dead letters en TODAS las subscriptions (incl. consumidor y smoke-tests).
- 3 queries custom (union de traces/requests/exceptions/dependencies/customEvents, hasta 12h):
  CERO telemetria del SDK. La app esta Running pero el SDK no emite (sampling/flush no alcanza
  bajo saturacion; host.json tiene samplingSettings con maxTelemetryItemsPerSecond=5).

### Metricas de plataforma (Azure Monitor, independientes del SDK) - EVIDENCIA DURA
- `Requests` (HTTP recibidas): 37, 34, 28, 9 por bin de 30m (21:30-23:00). La app SI recibe trafico.
- `FunctionExecutionCount`: 24, 4, 4, 9 (22:30-23:15). SI ejecuta funciones.
- `Http5xx`: VACIO. `Http2xx`: 22, 4, 1 -> muchas menos respuestas 2xx que requests recibidas.
- `HttpResponseTime` maximo: **101.3s, 204.9s, 103.6s** (22:30-23:15). Supera el timeout de 100s
  del HttpClient -> el cliente cancela -> `TaskCanceledException` (Cluster B exacto).
- Service Bus `IncomingMessages`: 2, 3, 6 / `OutgoingMessages`: 2, 1, 3 (22:45-23:15). Los mensajes
  del smoke test SI llegan, pero salen menos de los que entran -> backlog por consumo lento.
- `MemoryWorkingSet`: pico ~442 MB. No es saturacion de memoria.

### Infraestructura
- Function App `Running`, `enabled`, `availabilityState: Normal`. App settings obligatorios OK
  (dotnet-isolated, ~4, placeholder=1, run-from-package=1). App Insights conectado al recurso correcto.
- Plan: **B1 (Basic, 1 core, 1 instancia)**. Cumple ADR-0020 (>= B1).
- host.json: `maxConcurrentCalls: 1`, `prefetchCount: 0` (viene del scaffold + fix #50). ADR-0017.

## Diagnostico
**Causa raiz unica para ambos clusters: saturacion de throughput de la Function App, NO el
deserializador ni #165.**

La combinacion B1 (1 core) + `maxConcurrentCalls: 1` procesa requests HTTP y mensajes de Service
Bus de a UNO, en serie. Bajo la rafaga del smoke test (HTTP + SB simultaneos) cada operacion
encola detras de otra y la latencia se dispara a >100s:
- Cluster B: las requests HTTP de `RegistrarMarcacion` no responden en 100s -> `TaskCanceledException`.
  Confirmado por `HttpResponseTime` maximo > 100s y ausencia de Http5xx.
- Cluster A: el mensaje `ProgramacionTurnoDiarioSolicitada` llega al SB pero el consumidor (mismo
  worker saturado, `maxConcurrentCalls: 1`) no lo procesa dentro del timeout de 30s del smoke test.
  `turno_diario_asignado` aun no esta escrito cuando el test verifica. Sin DLQ ni excepciones porque
  el mensaje no falla: solo espera en cola.

**No es regresion de #165.** El deserializador no fue tocado; el cambio de #165 no lanza y se commitea
atomicamente. La causa es de capacidad/configuracion, preexistente (host.json del scaffold + plan B1).
#165 pudo *agravar* marginalmente la latencia (cada mensaje ahora publica DiaCalculado adicional via
outbox), pero no es el origen.

## Acciones
Issues propuestos (NO creados, pendiente de validacion del usuario):
1. tipo:infra/tooling - Subir `maxConcurrentCalls` (ej. 4-8) y `prefetchCount` proporcional
   (guia MS: maxConcurrentCalls x 20) en host.json de ControlHoras; revisar ADR-0017 (asume volumen
   bajo y secuencial; los smoke tests concurrentes rompen ese supuesto). Evaluar lock_duration.
2. tipo:infra - Evaluar escalar el plan o numberOfWorkers de B1 para dev, o reducir la concurrencia
   del runner de smoke tests / subir timeouts (HttpClient 100s, polling SB 30s) para tolerar B1.
3. tipo:tooling - Bug menor en `appinsights-query.sh function-status`: concatena los nombres de las
   Function Apps en un solo recurso ("func-...-control-horas func-...-programacion") -> ResourceNotFound.

## Preguntas abiertas
- Por que el SDK de App Insights no emitio NADA en 12h pese a estar conectado y la app Running.
  Hipotesis: sampling + flush bajo saturacion + worker isolated bloqueado. Requiere monitoreo:
  revisar si con la app ociosa la telemetria vuelve a fluir (descartar telemetria rota de fondo).
- Cuanta concurrencia real soporta B1 sin degradar (1 core). Definir el target de `maxConcurrentCalls`
  con una prueba de carga controlada antes de fijar el valor.
- Confirmar si el runner de smoke tests lanza los tests en paralelo (xUnit parallelization), lo que
  amplificaria la rafaga contra un worker de concurrencia 1.
