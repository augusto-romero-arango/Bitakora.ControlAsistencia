# CA-ADR-0009: Control de costos de Application Insights

## Estado

Aceptado

## Contexto

Un incidente en produccion donde una funcion con errores genero millones de registros en 3 dias
resulto en un costo inesperado de $350 USD por Application Insights. El sistema no tenia limites
de ingestion, alertas de costos, ni configuracion de log levels. El sampling adaptativo usaba
defaults permisivos (20 items/seg) y los frameworks Marten y Wolverine generaban logs
Information extremadamente verbosos durante los retries internos.

El sistema maneja hasta 50.000 empleados con picos de carga en marcaciones. La observabilidad
debe ser suficiente para depurar errores sin generar costos impredecibles.

## Decision

Se implementa una estrategia de control de costos en 4 capas defensivas:

**Capa 1 - Log Levels (host.json)**: nivel default en Warning, solo el codigo de negocio
(`Function`) en Information. `Marten` y `Wolverine` explicitamente en Warning para evitar
el torrente de logs por query SQL y procesamiento de envelopes en escenarios de retry.

**Capa 2 - Sampling (host.json)**: adaptativo con limite de 5 items/seg (vs 20 por defecto).
`Request` y `Event` excluidos del sampling porque ya estan cubiertos por `Host.Results`.
Las excepciones nunca se muestrean (comportamiento por defecto de Application Insights) —
la capacidad de depurar errores no se ve afectada.

**Capa 3 - Daily Cap (Terraform)**: 0.5 GB/dia en Application Insights (~$35/mes maximo).
Notificaciones de cap habilitadas. En el incidente, la ingestion fue ~50 GB/dia durante 3 dias.

**Capa 4 - Alertas (Terraform)**: dos alertas con notificacion por email:
- Ingestion diaria supera el 80% del daily cap (evaluada cada hora)
- Pico de excepciones >50 en 5 minutos (evaluada cada 5 min) — detecta el patron exacto
  del incidente: funcion en loop de errores

El paquete `Azure.Monitor.OpenTelemetry.AspNetCore` se remueve del csproj de cada dominio
porque `UseAzureMonitor()` no se invoca y el host de Azure Functions ya maneja la telemetria
automaticamente via `APPLICATIONINSIGHTS_CONNECTION_STRING`. Mantener el paquete crea riesgo
de doble ingestion si se activa sin configurar sampling.

## Consecuencias

**Positivas**

- Costo mensual predecible: maximo ~$35/mes por Application Insights en el peor caso
  (vs $350 en 3 dias antes del cambio).
- Las excepciones se capturan al 100% — la capacidad de depurar errores no se ve afectada.
- La alerta de excepciones detecta funciones en loop de errores en 5 minutos.
- La alerta de ingestion da margen de reaccion antes de que el daily cap corte la ingestion.
- El domain-scaffolder genera nuevos dominios con estos controles por defecto.

**Negativas**

- Los logs Information de Marten y Wolverine no estan disponibles en produccion.
  Para depurar problemas especificos de estas librerias, se puede ajustar el log level
  temporalmente en staging.
- Si el daily cap se alcanza (0.5 GB), se pierde telemetria hasta el dia siguiente.
  La alerta al 80% mitiga esto dando tiempo para investigar antes del corte.
- El sampling a 5/seg reduce la granularidad para analisis de rendimiento en picos.
  Para analisis de performance detallado, se puede subir temporalmente.

## Valores por ambiente

| Parametro | dev | staging | prod |
|---|---|---|---|
| `daily_data_cap_in_gb` | 0.5 | 1.0 | 2.0 |
| `maxTelemetryItemsPerSecond` (clasico) | 5 | 5 | 10 |
| `TELEMETRY_SAMPLING_RATIO` (OpenTelemetry) | 0.2 | 0.2 | 0.1 |
| Exception spike threshold | 50 | 100 | 200 |

## Actualizacion (2026-06-18): Capa 2 bajo OpenTelemetry

Durante la investigacion de los smoke tests rojos del PR #165 se detecto que el proceso worker
de las Function Apps **no emitia telemetria** (tablas `requests`/`dependencies`/`exceptions` en 0):
el pipeline OpenTelemetry registraba fuentes pero sin exporter, asi que los spans se descartaban y
no habia desglose de latencia por capa para depurar. Se instrumento el worker de ControlHoras con
`telemetryMode: OpenTelemetry` (host.json) + `UseFunctionsWorkerDefaults()` + `UseAzureMonitorExporter()`.

Esto **cambia el mecanismo de la Capa 2**: segun la doc oficial de Microsoft, con
`telemetryMode: OpenTelemetry` la seccion `logging.applicationInsights` de host.json (donde vivia
`maxTelemetryItemsPerSecond`) **deja de aplicar**. El control de volumen pasa a un sampler OTel
head-based: `ParentBased(TraceIdRatioBased(ratio))`, con el ratio configurable via la app setting
`TELEMETRY_SAMPLING_RATIO` (default 0.2 en codigo). Para un diagnostico puntual se sube a 1.0 y se
restaura despues.

**Trade-off honesto respecto a la decision original**: el sampling head-based muestrea tambien las
excepciones, a diferencia del sampling adaptativo clasico de Application Insights que las preservaba
al 100%. Mitigaciones:
- La **Capa 3 (daily cap)** sigue intacta como tope duro de costo (0.5 GB/dia dev) -- un dia de $300
  sigue siendo estructuralmente imposible.
- La **Capa 4 (alerta de spike de excepciones)** sigue activa, pero el sampling reduce su
  sensibilidad: por eso el ratio no debe ser demasiado bajo.
- La **Capa 1 (logLevel de host.json)** solo filtra logs del proceso host; los logs del worker se
  filtran con configuracion OTel del worker (pendiente si se requiere mayor control).

Capas 3 y 4 sin cambios. Esta adaptacion aplica al dominio ControlHoras; los demas dominios
mantienen el modelo clasico hasta que se instrumenten igual.

## Actualizacion (2026-08-04, issue #308): la Capa 2 descrita arriba nunca estuvo activa

La actualizacion anterior describia la Capa 2 bajo OpenTelemetry como
`ParentBased(TraceIdRatioBased(ratio))`, wireado con `SetSampler(...)` **antes** de
`UseAzureMonitorExporter()`. Midiendo 24h de ingestion real en dev se encontro que ese sampler
**nunca llegaba a instalarse**: `UseAzureMonitorExporter()` (`Azure.Monitor.OpenTelemetry.Exporter`
1.8.1) llama `SetSampler(...)` internamente, y como `AzureMonitorExporterOptions.TracesPerSecond`
tiene default `5.0`, siempre construye un `RateLimitedSampler` que pisa al sampler que este seam
configuraba. Verificado en runtime leyendo el sampler efectivo del `TracerProvider` resuelto:

```
Sampler efectivo con el wiring anterior: Azure.Monitor.OpenTelemetry.Exporter.Internals.RateLimitedSampler
Sampler que el codigo del proyecto pretendia instalar: OpenTelemetry.Trace.ParentBasedSampler
```

Consecuencia medida: el "sampler de ratio 0.2" nunca recortaba nada. Lo que realmente corria era un
techo de 5 traces/s (~432k/dia), y los procesos operaban muy por debajo de ese techo (~1,4-1,6/s),
asi que **no habia sampling efectivo en absoluto** desde que se adopto OpenTelemetry -- toda la
telemetria de trazas se exportaba integra. `TELEMETRY_SAMPLING_RATIO` era, en la practica, una
variable inerte: aunque se hubiera puesto en algun recurso (no lo estaba), el valor se calculaba y
se descartaba.

**Correccion (de orden, no de contenido).** `SetSampler(...)` del proyecto debe ejecutarse
**despues** de `UseAzureMonitorExporter()`, no antes. En la forma `IOpenTelemetryBuilder` esto se
expresa encadenando un **segundo** `.WithTracing(...)` posterior al `.UseAzureMonitorExporter()`
(cada llamada a `WithTracing` registra un callback de configuracion sobre el mismo
`TracerProviderBuilder`; el que se registra al final es el que gana). El sampler sigue siendo
`ParentBased(TraceIdRatioBased(ratio))`, ratio configurable via `TELEMETRY_SAMPLING_RATIO` (default
0.2) -- ahora si efectivo.

**El orden es fragil por naturaleza, asi que queda protegido por guardrail y no por convencion.**
Cualquier reordenamiento futuro (o una version del exporter que cambie cuando registra su sampler)
volveria a dejar el sampler del proyecto sin instalar, compilando y con los tests unitarios en
verde: es el modo de falla que costo dos meses de ingestion completa. Los tests de composicion de
ambos procesos (`ConfiguracionObservabilidadProjectionsTests`, `ComposicionServiciosTests`,
MEF-ADR-0029) leen el sampler **efectivo** del `TracerProvider` resuelto del contenedor y afirman
que no es `RateLimitedSampler` y que el ratio -- configurado y por defecto -- llego hasta el. La
tecnica es determinista: compara tipos y lee `Sampler.Description`, sin muestrear actividades contra
un ratio fraccionario. Esto deroga el "limite conocido" que esos archivos declaraban antes ("el
sampler compuesto vive dentro de `TracerProviderSdk`, que OpenTelemetry no expone publicamente...
queda cubierto por revision de codigo"): la revision de codigo no podia atrapar este defecto, porque
el codigo visible era correcto.

**Estado real de `TELEMETRY_SAMPLING_RATIO` frente a la tabla "Valores por ambiente" de arriba.** La
variable no esta declarada en Terraform en **ningun** ambiente (verificado con `az functionapp config
appsettings list`), asi que los valores de esa fila expresan la intencion de diseno, no lo que corre:
hoy los tres ambientes usan el default de codigo (0.2). Declararla por ambiente queda fuera del
alcance del issue #308 a proposito -- recalibrar el ratio se decide **despues** de medir con el
sampler ya efectivo, no antes.

**Alternativa considerada y descartada: configurar `AzureMonitorExporterOptions` en vez de
reordenar.** Poner `o.TracesPerSecond = null; o.SamplingRatio = ratio;` hace que
`UseAzureMonitorExporter()` instale `ApplicationInsightsSampler` en vez de `RateLimitedSampler`. Es
la via idiomatica del exporter y tiene una ventaja real: `ApplicationInsightsSampler` estampa el
campo `sampleRate` en cada item para que Application Insights **extrapole** los conteos (a
diferencia de `TraceIdRatioBasedSampler`, que subcuenta las metricas sin ese campo). Se descarto
porque `ApplicationInsightsSampler` es `internal`
(`Azure.Monitor.OpenTelemetry.Exporter.Internals`) y no se puede componer con el filtro de la
siguiente seccion (envolverlo para descartar un span por nombre). **Se asume la subcuenta de
metricas** como consecuencia conocida de esta decision: los conteos de trazas en Application
Insights para estos dos procesos no reflejan el volumen real por encima de 1/ratio -- solo el
volumen efectivamente exportado, sin extrapolar.

## Actualizacion (2026-08-04, issue #308): filtro del span de polling del daemon (worker de Projections)

El worker de proyecciones (`Bitakora.ControlAsistencia.Projections`) corre el daemon HotCold de
Marten, que emite una actividad `marten.daemon.highwatermark` cada ~5s sin valor diagnostico. Con
la Capa 2 corregida (arriba) ese span pasa a competir por el mismo ratio de sampling que las
actividades con valor real, y su hijo Postgres (`Npgsql`) explicaba el 95% de los spans Postgres del
worker en la medicion de 24h que motivo este issue.

Se agrego `SamplerQueDescartaPollingDelDaemon`, que envuelve el sampler de ratio y descarta esa
actividad especifica por nombre (`Drop`, sin delegar) dejando pasar el resto de la fuente `Marten`
sin alterar el ratio configurado. Un `Drop` en la actividad raiz basta para que su hija Npgsql ni
siquiera se instancie (verificado empiricamente: `ParentBasedSampler` resuelve al hijo por el
contexto del padre sin `Recorded`, y devuelve `AlwaysOffSampler`) -- no hace falta un
`BaseProcessor<Activity>` adicional para filtrar el hijo por separado.

Este filtro **solo se aplica en el worker de Projections** (MEF-ADR-0018, Rule of Three): es el unico
proceso que corre el daemon HotCold. `ControlHoras` y `Programacion` no lo necesitan -- no emiten
ese span -- y no se generaliza el wrapper a ellos hasta que exista un segundo consumidor real.

Capas 3 y 4 sin cambios.

## Actualizacion (2026-08-15): la Capa 4 se acota a las excepciones con respuesta 500

La alerta de spike (`<prefijo>-exception-spike`) contaba **toda** excepcion registrada en la ventana
de 5 minutos. Se acota a las excepciones cuya request HTTP termino en **status code 500**.

**Motivo.** El resto del ruido de la tabla `exceptions` no describe el fallo que la alerta existe
para atrapar. Bajo CA-ADR-0030, una violacion de regla de negocio en un comando HTTP se declina con
un resultado que el handler traduce a 409/404 -- no lanza y no persiste evento de fallo. Un 500 es,
por construccion, un fallo tecnico no manejado: exactamente el patron "funcion en loop de errores"
del incidente que motivo este ADR.

**Implementacion.** `exceptions` no expone el status code -- vive en `requests`. La consulta
correlaciona ambas tablas por `operation_Id` (el id de traza compartido entre el request y la
excepcion que lo hizo fallar), en `infra/modules/monitoring/main.tf`:

```kql
let operacionesCon500 =
    requests
    | where timestamp > ago(5m)
    | where resultCode == "500"
    | distinct operation_Id;
exceptions
| where timestamp > ago(5m)
| where operation_Id in (operacionesCon500)
| summarize ExceptionCount = count()
| where ExceptionCount > 50
```

Umbral (>50), frecuencia (PT5M), ventana (PT5M), severidad (1) y action group: sin cambios.

**Consecuencia: la alerta deja de cubrir los triggers de Service Bus, y la cobertura se repone con
una alerta aparte** (siguiente seccion). No es una degradacion parcial sino cobertura cero por este
camino: los triggers no-HTTP de Azure Functions reportan `resultCode` **`0`**, no un status HTTP, asi
que ninguna invocacion de un consumidor de eventos entra jamas por el filtro `resultCode == "500"`.
El filtro no "pierde sensibilidad" en el bus -- simplemente no lo ve. La via correcta es una segunda
alerta, no relajar el filtro de esta.

**Alcance del filtro: 500 exacto, no 5xx.** `resultCode == "500"` deja fuera 502/503/504, que en
Azure Functions vienen de la plataforma (timeouts, arranque en frio, saturacion del plan) y no de
codigo en loop. Ampliar a `toint(resultCode) >= 500` es un cambio de una linea si se decide que esos
casos tambien deben despertar a alguien.

Capas 1, 2 y 3 sin cambios.

## Actualizacion (2026-08-15): la Capa 4 suma una alerta para los consumidores de Service Bus

La Capa 4 pasa a tener **dos** alertas de spike, una por borde:

| Alerta | Cubre | Filtro |
|---|---|---|
| `<prefijo>-exception-spike` | Borde HTTP | excepciones correlacionadas a un request con `resultCode == "500"` |
| `<prefijo>-non-http-failure-spike` | Triggers no-HTTP | invocaciones con `success == false` cuyo `resultCode` no es un status HTTP |

Umbral (>50), frecuencia (PT5M), ventana (PT5M), severidad (1) y action group: iguales en ambas.

**Se alerta sobre invocaciones fallidas, no sobre la metrica `DeadletteredMessages`.** La razon es
una limitacion dura de Azure Monitor, no una preferencia: esa metrica solo se desglosa por la
dimension `EntityName`, que es la **queue o el topic** -- no existen metricas por subscription. Y en
este proyecto la subscription `smoke-tests` vive **dentro de los mismos topics de negocio**
(`programacion-turno-diario-solicitada`, `dia-calculado`, `registro-de-marcacion-creado`), junto a
las subscriptions `control-horas-escucha-*`. Una alerta metrica sumaria el DLQ de los smoke tests al
de los consumidores reales bajo el mismo `EntityName`, **sin ninguna forma de separarlos**.

La consulta sobre `requests` no tiene ese problema y no necesita lista de exclusion: la subscription
`smoke-tests` no tiene Function App consumidora -- nadie la procesa, asi que no genera invocaciones.
Sus dead letters los produce el propio proceso de smoke tests corriendo en CI, que no reporta a
Application Insights. Lo que si cuenta, y debe contar, es una funcion real fallando al procesar un
mensaje publicado por un smoke test: eso es un error del sistema, no ruido del arnes.

**Ventaja secundaria: detecta antes.** Un dead letter solo aparece tras agotar `max_delivery_count`
(10 entregas); la invocacion fallida se registra en el primer intento.

**Como se identifica un trigger no-HTTP.** Los triggers no-HTTP de Azure Functions reportan
`resultCode` `"0"`. La consulta no compara contra `"0"` -- valor no contractual, sujeto a cambio del
host -- sino que descarta lo que si es un status HTTP: `isnull(toint(resultCode)) or
toint(resultCode) !between (100 .. 599)`. Asi la alerta cubre cualquier trigger no-HTTP que se
agregue despues (timer, blob) sin tocar la consulta.

**Limitacion conocida: el sampling head-based tambien recorta esta alerta.** Igual que la de
excepciones, opera sobre telemetria muestreada al ratio de la Capa 2 (0.2 por defecto), asi que el
conteo observado es una fraccion del real. Vale la misma mitigacion: el ratio no debe bajar mas sin
recalibrar los umbrales de ambas alertas.

**Alternativa considerada y descartada: alerta metrica sobre `DeadletteredMessages` del topic.**
Descartada porque no puede excluir el ruido de `smoke-tests` (arriba). Para habilitarla haria falta
cambiar primero la infraestructura -- mover los smoke tests a un namespace o a topics propios -- lo
que duplicaria la topologia de eventos solo para observabilidad. Si en el futuro se quiere el DLQ
como red de seguridad de ultimo recurso, la decision de topologia va antes que la alerta.

Capas 1, 2 y 3 sin cambios.

## Actualizacion (2026-08-17, issue #398): la Capa 4 suma una tercera alerta para el worker de proyecciones

Acotar la alerta de excepciones al borde HTTP (seccion anterior) dejo un hueco: el worker de
proyecciones (`Bitakora.ControlAsistencia.Projections`) no atiende HTTP -- no genera `requests` con
`resultCode == "500"` -- y sus operaciones no son un trigger no-HTTP de Azure Functions (la segunda
alerta mira `requests` fallidos de un host de Functions; el daemon HotCold de Marten corre spans
internos que van a `dependencies`/`exceptions`, no a `requests`). Antes de acotar la Capa 4 por
borde, sus excepciones al menos contaban en la alerta generica de spike; despues, un worker en loop
de errores no despertaba a nadie -- exactamente el patron que motivo este ADR.

La Capa 4 pasa a tener **tres** alertas de spike, una por borde:

| Alerta | Cubre | Filtro |
|---|---|---|
| `<prefijo>-exception-spike` | Borde HTTP | excepciones correlacionadas a un request con `resultCode == "500"` |
| `<prefijo>-non-http-failure-spike` | Triggers no-HTTP | invocaciones con `success == false` cuyo `resultCode` no es un status HTTP |
| `<prefijo>-projections-exception-spike` | Worker de proyecciones | excepciones con `cloud_RoleName == "Bitakora.ControlAsistencia.Projections"` |

Umbral (>50), frecuencia (PT5M), ventana (PT5M), severidad (1) y action group: iguales en las tres.

```kql
exceptions
| where timestamp > ago(5m)
| where cloud_RoleName == "Bitakora.ControlAsistencia.Projections"
| summarize ExceptionCount = count()
| where ExceptionCount > 50
```

**Por que filtrar por `cloud_RoleName` y no correlacionar con `requests` (como la alerta del borde
HTTP).** El worker no es un host de Azure Functions -- no tiene requests que correlacionar. El valor
exacto (`"Bitakora.ControlAsistencia.Projections"`) es la constante `NombreServicio` de
`ConfiguracionObservabilidadProjections.cs`, fijada por guardrail de test (issue #263): un cambio de
ese string sin actualizar la alerta la deja evaluando un `cloud_RoleName` que ya no existe, silenciosa
sin ningun error visible. No se generaliza a un filtro que cubra "cualquier worker no-HTTP" (MEF-ADR-0018,
Rule of Three): `Projections` es hoy el unico proceso que no es ni borde HTTP ni trigger de Azure
Functions.

**Por que esta alerta no excluye trafico de smoke tests (a diferencia de nada -- nunca hubo lista de
exclusion que quitarle).** Los smoke tests publican eventos validos: sus propias aserciones exigen
cero dead letters, asi que no pueden inducir errores en el worker. Toda excepcion del worker es senal
real y accionable, venga de un evento de negocio o de un evento publicado por un smoke test -- a
diferencia de la segunda alerta (seccion anterior), que si necesita descartar la subscription
`smoke-tests` porque esa si comparte `EntityName` con subscriptions de negocio en la metrica de DLQ.
Aqui no aplica esa limitacion: la fuente es `exceptions` filtrada por rol de servicio, no una metrica
por `EntityName`.

**Limitacion conocida: el sampling head-based tambien recorta esta alerta.** Misma limitacion que las
dos alertas hermanas: opera sobre telemetria muestreada al ratio de la Capa 2 (`TELEMETRY_SAMPLING_RATIO`,
0.2 por defecto), asi que el conteo observado es una fraccion del real. No bajar el ratio sin
recalibrar el umbral de las tres alertas.

> **Corregido por la actualizacion (2026-08-17, issue #412) al final de este ADR.** El experimento de
> falla inducida midio que el ratio 0.2 **no** recorta esta alerta: las excepciones del daemon viajan
> por la senal de logs con `SpanId == default` y pasan enteras (87 de 87, 1:1). El recorte real es
> binario y de otro origen -- el sampler del issue #308 suprime al 100% una familia de errores del
> daemon. El parrafo de arriba se conserva por trazabilidad; la seccion final manda.

Capas 1, 2 y 3 sin cambios.

## Actualizacion (2026-08-17, issue #412): verificacion empirica de la alerta del worker

La alerta de la seccion anterior se desplego con ruido cero verificado (sin falsos positivos), pero
nunca se comprobo su **capacidad de deteccion**: la tabla `exceptions` estaba vacia para el worker en
los 30 dias de retencion, y la cadena "daemon falla -> excepcion visible en la alerta" era doctrina
asumida. Se ejecuto una falla inducida sobre dev en sesion supervisada.

**Protocolo.** `az postgres flexible-server stop` sobre `psql-asist-dev` (17:02:18Z, `Stopped` a las
17:06:27Z) y `start` (17:12:14Z, `Ready` a las 17:20:28Z). Se eligio detener el servidor y no romper la
connection string del Container App: el provider `azurerm` no modela el power state, asi que no hay
drift de Terraform, y el worker no re-arranca -- el proceso vivo pierde la conexion y el daemon entra
en reintentos, que es la cadena a verificar. `TELEMETRY_SAMPLING_RATIO` no se toco (ausente en el
Container App, o sea el default 0.2 real).

**Resultado 1 -- la alerta detecta, pero su umbral es inalcanzable.** La query exacta de la alerta
devolvio 88 filas sobre la ventana de falla: el filtro por `cloud_RoleName` funciona y la alerta no es
ciega. Pero evaluada en bins de 5 minutos -- que es su ventana real -- el maximo fue **30**, contra un
umbral de `>50`:

| Ventana (UTC) | Excepciones del worker | Supera >50 |
|---|---|---|
| 17:00 | 7 | no |
| 17:05 | 26 | no |
| 17:10 | 25 | no |
| 17:15 | 30 | no |

Una caida total de Postgres de ~14 minutos -- el peor caso realista para el worker -- **no dispara la
alerta**. El ritmo del daemon esta acotado por su backoff de reintentos y por el numero de shards (hoy
3: `FichaColaborador:All`, `CategoriaDeEtiquetas:All`, `TurnoVigente:All`), no por la gravedad del
fallo. El umbral >50 se heredo de las alertas de borde HTTP, donde un loop de reintentos si produce
cientos de eventos por minuto; el daemon no tiene ese perfil. Recalibrar es un issue aparte.

**Resultado 2 -- la via es la senal de logs, y el ratio 0.2 no la recorta.** Evidencia por fila en
`exceptions`: `customDimensions.CategoryName == "Marten.Events.Daemon.Coordination.ProjectionCoordinator"`
y `customDimensions.OriginalFormat` con la plantilla del log -- ambos campos solo existen si el item
nacio de un `LogRecord` de `ILogger`, no de un span event. Ademas `operation_Id` **vacio** en las 88
filas: el `LogError` del daemon ocurre fuera de un span activo (`SpanId == default`). Eso es lo que
neutraliza el recorte: `AzureMonitorExporterOptions.EnableTraceBasedLogsSampler` viene en `true` por
defecto (exporter 1.8.1) e instala un `LogFilteringProcessor` que descarta el LogRecord solo si tiene
`SpanId` y su span no fue muestreado; sin `SpanId` pasa siempre. Sin sampling de ingestion tampoco
(`itemCount == 1` en las 88 filas, `samplingPercentage == 100`).

Contraste consola vs `exceptions` sobre la ventana completa, por familia de mensaje:

| Familia de error del daemon | Consola | `exceptions` | Ratio |
|---|---|---|---|
| `Error trying to attain a lock for set {Name}...` | 87 | 87 | **1:1** |
| `Failed while trying to detect high water statistics...` | 35 | 0 | **0%** |

**Resultado 3 (hallazgo no buscado) -- el sampler del issue #308 ciega una familia entera de errores.**
El split no es fraccional, es binario, y tiene una causa exacta. `SamplerQueDescartaPollingDelDaemon`
devuelve `SamplingDecision.Drop` para el span `marten.daemon.highwatermark`; el `HighWaterAgent` de
JasperFx emite sus `LogError` **dentro** de ese span, con `SpanId` poblado y `TraceFlags != Recorded`.
El `LogFilteringProcessor` los descarta. Los 35 errores de high water llevaban excepcion con stack
trace completo -- eran candidatos legitimos a `exceptions` -- y no llegaron a ninguna tabla.

El sampler se escribio para recortar **costos de trazas** (95% de los spans Postgres del worker cuelgan
de ese polling). El efecto colateral es que suprime tambien los **logs de error** emitidos bajo ese
span, que era justo lo que no se queria perder. Es el mismo modo de falla que el issue #308 corrigio
--- wiring a medio terminar, silencioso --- reaparecido un nivel mas abajo. El desacople existe y no
cuesta trazas: `.UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)`.

**Correccion a la seccion anterior.** La afirmacion "el sampling head-based tambien recorta esta
alerta" es **incorrecta** para el worker en su mecanismo y en su magnitud: no hay recorte proporcional
al ratio 0.2 sobre las excepciones del daemon. Hay paso integro (1:1) para los errores sin contexto de
span y supresion total (0%) para los emitidos bajo el span de polling. La consecuencia practica se
invierte: bajar `TELEMETRY_SAMPLING_RATIO` no vuelve mas insensible esta alerta, y subirlo no la
arregla.

**Efecto colateral observado en las alertas hermanas.** Durante la ventana, `func-asist-dev-colaboradores`
y `func-asist-dev-control-horas` generaron 810 excepciones de `Wolverine.RDBMS.DurabilityAgent` e
`IAgentCommand` (116-120 por ventana de 5 min, holgadamente sobre 50, tambien con `operation_Id` vacio).
Ninguna alerta disparo: hubo **cero `requests`** en toda la ventana, y las dos alertas de borde se
condicionan a `requests` (500 HTTP / invocaciones no-HTTP fallidas). Un fallo en un agente de fondo no
produce requests, asi que esas dos alertas son estructuralmente ciegas a este modo de falla. Total del
incidente: ~1000 excepciones en tres roles, **cero alertas disparadas** (verificado contra
`Microsoft.AlertsManagement/alerts`).

**Reversion (verificada).** Postgres `Ready`; la replica del worker
(`ca-asist-dev--0000033-84b48c4f4b-xwtpq`, creada 00:20:28Z) nunca re-arranco, confirmando la premisa
del mecanismo elegido; post-recuperacion 3 dependencias Npgsql exitosas y 0 excepciones; las tres
Function Apps `Running`.

Capas 1, 2 y 3 sin cambios. La Capa 4 conserva sus tres alertas; lo que cambia es lo que se sabe de
ellas.

## Actualizacion (2026-08-27, issue #414): desacople del muestreo de logs del de trazas (worker)

El resultado 3 del #412 se cierra: `EnableTraceBasedLogsSampler` queda en `false` en
`ConfiguracionObservabilidadProjections.ConfigurarObservabilidad`, con guardrail de composicion que
lee las opciones efectivas del contenedor (`IOptionsMonitor<AzureMonitorExporterOptions>`) para que
un cambio futuro del default del paquete, o un refactor del seam, no lo reactive en silencio.

**Decision.** Flag apagado + filtro de nivel por proveedor sobre las categorias del daemon
(`JasperFx*` / `Marten*`), no flag solo. Apagar unicamente el flag habilita el paso de TODOS los
`LogRecord` del daemon HotCold hacia el exporter -- incluidos sus `Information` rutinarios
("Executed updates for Event range...", emitidos cada pocos segundos con `min_replicas = 1`, 24/7,
a diferencia de las Function Apps que escalan a cero) -- presionando la Capa 3 (daily cap) en la
direccion opuesta a la que este issue busca. El filtro sube el piso de EXPORTACION de esas
categorias a `Warning`: los `LogError` que este cambio rescata (`Error > Warning`) se conservan, el
ruido `Information` no. Se implemento con `AddFilter<OpenTelemetryLoggerProvider>(categoria,
LogLevel.Warning)` sobre el `ILoggerProvider` que `UseAzureMonitorExporter` instala
(`OpenTelemetry.Logs.OpenTelemetryLoggerProvider`, verificado por reflection contra
`Azure.Monitor.OpenTelemetry.Exporter` 1.8.1 / `OpenTelemetry.Extensions.Hosting` 1.16.0) -- no toca
`host.json`/`appsettings.json`, asi que la consola del Container App conserva `Information` intacto
(fue la senal que permitio diagnosticar el bug en el #412).

**Consecuencia operativa del overload con callback.** `UseAzureMonitorExporter()` sin argumentos
registraba `DefaultAzureMonitorExporterOptions`, que lee `APPLICATIONINSIGHTS_CONNECTION_STRING`
directo del entorno; el overload con callback no lo registra. La connection string sigue llegando
-- via `IConfiguration`, que el proveedor de variables de entorno de `Host.CreateApplicationBuilder`
del worker puebla, de modo que la Key Vault reference que inyecta el Container App
(MEF-ADR-0025/CA-ADR-0026) no cambia --, pero ese camino pasa a ser el unico: un host sin ese
proveedor apagaria la exportacion completa en silencio, y los guardrails que solo resuelven el
`TracerProvider` del contenedor seguirian verdes. Queda fijado con un guardrail propio
(`ConfigurarObservabilidad_ResuelveLaConnectionStringDelEntorno_EnLasOpcionesDelExporter`) que
compara la connection string RESUELTA en las opciones efectivas contra la variable de entorno.

**Alcance: solo el worker (Rule of Three, MEF-ADR-0018).** Los tres Function Apps NO exhiben este
modo de perdida: sus 810 excepciones del experimento #412 (`Wolverine.RDBMS.DurabilityAgent` /
`IAgentCommand`) SI llegaron a `exceptions` -- `operation_Id` vacio implica `SpanId == default`,
que el `LogFilteringProcessor` deja pasar sin condicion. El modo de perdida binario requiere un
sampler que descarte al 100% una familia de spans con `LogError` emitidos adentro, y ese sampler
(`SamplerQueDescartaPollingDelDaemon`, issue #308) solo existe en el worker. El gap de los Function
Apps es de alertas (las dos alertas de borde HTTP son estructuralmente ciegas a fallos de agentes de
fondo sin `requests`, ya documentado en la actualizacion del #412) -- lo cubre el issue #415, no
este.

**Verificacion empirica pendiente, delegada al issue #413.** Este cambio se valida por composicion
(guardrails de `ConfiguracionObservabilidadProjectionsTests`): que el flag efectivo sea `false` y que
el filtro suba el piso de exportacion del daemon a `Warning` conservando `Information` para el resto
del worker. Falta por medir en dev, con el mismo protocolo de caida inducida de Postgres del #412:
(a) que los `LogError` del `HighWaterAgent` efectivamente lleguen a `exceptions` con el flag apagado
(cerrar el 0% de ratio medido), y (b) el volumen exportado resultante contra el daily cap de la Capa
3, dato que tambien alimenta la recalibracion del umbral de la Capa 4 que el #413 hace en el mismo
paso.

Capas 1 y 3 sin cambios. La Capa 2 (sampling de trazas, `TELEMETRY_SAMPLING_RATIO`) tampoco cambia:
este desacople es exclusivamente del pipeline de logs -- las trazas siguen su propio sampler
(`SamplerQueDescartaPollingDelDaemon` envolviendo `ParentBasedSampler{TraceIdRatioBasedSampler}`),
sin tocar.

## Actualizacion (2026-08-30, issue #515): se suprime la exportacion de metricas OTel en las tres Function Apps

Investigacion del episodio de `ingestion-warning`: **`AppMetrics` consume 0.103 GB/dia (~23% del
daily cap de 0.5 GB)** en `customMetrics` que nadie consulta -- verificado por grep contra el repo
y el plugin `mefisto`: ni `/health-check`, ni `bug-investigator`, ni ninguna alerta de las tres de
la Capa 4 las lee (todas usan `exceptions`/`requests`). Origen medido (24h, por rol): las tres
Function Apps que ya adoptaron el exporter completo (Colaboradores, ControlHoras, Sedes) emiten
~76.000 metricas/dia cada una, activadas automaticamente por `UseAzureMonitorExporter()` -- en
`ComposicionServicios.cs` de cada dominio solo estaba configurado `.WithTracing(...)`, sin ningun
`.WithMetrics(...)` explicito. Familias observadas: `_APPRESOURCEPREVIEW_` (latido del propio
exporter OTel, identificado por `customDimensions.telemetry.sdk.name: opentelemetry`),
`dotnet.gc.*`, `http.server.active_requests`, `kestrel.*`, `azure.functions.health_check.reports`,
`*.cpu.time`. Es telemetria de capacidad -- valiosa en procesos de larga vida bajo carga real, ruido
caro en Function Apps de dev que escalan a cero. `Programacion` (sin exporter todavia) queda fuera
de alcance; cuando lo adopte, debe nacer ya con este recorte.

**Decision: recortar TODO, no una familia puntual.** El daily cap (Capa 3) se mantiene en 0.5 GB --
con el recorte, el peor dia medido (0.454 GB) habria quedado en ~0.35 GB, bajo el umbral del 80% de
la Capa 4.

**Mecanismo: `AddView` con wildcard, no un opt-out del exporter.** `UseAzureMonitorExporter()`
(`Azure.Monitor.OpenTelemetry.Exporter` 1.8.1) no tiene overload ni opcion para excluir la senal de
metricas -- su xmldoc es explicito: "adding the Azure Monitor exporter for logging, distributed
tracing, and metrics" siempre las tres juntas. La supresion se implementa con un segundo
`.WithMetrics(metrics => metrics.AddView(instrumentName: "*", MetricStreamConfiguration.Drop))`
encadenado tras `.UseAzureMonitorExporter()` en el mismo seam de cada `ComposicionServicios.cs`. El
wildcard cubre cualquier instrumento, incluidos los que un futuro upgrade del exporter agregue sin
que nadie lo anticipe -- verificado que `WildcardHelper` (usado por `AddView` para el matching de
`instrumentName`) esta presente en `OpenTelemetry` 1.16.0 por grep de strings sobre el ensamblado.

**Hallazgo no anticipado: el reader de metricas de Azure Monitor exige connection string de forma
sincronica y sin tolerancia, a diferencia del de trazas.** Decompilando
`OpenTelemetryBuilderExtensions.UseAzureMonitorExporter` (`ilspycmd` sobre el ensamblado 1.8.1) se
confirma que el exporter de TRAZAS se agrega via un `ExporterRegistrationHostedService` -- diferido
al arranque real del host (`IHost.StartAsync`/`RunAsync`), que nunca corre al solo construir un
`ServiceProvider` de prueba, asi que su ausencia de connection string pasa inadvertida en ese
escenario. El reader de METRICAS, en cambio, se agrega de forma SINCRONICA dentro del callback de
`ConfigureOpenTelemetryMeterProvider` que arma la `MeterProviderSdk`: construye
`new AzureMonitorMetricExporter(options)` inmediatamente al resolver `MeterProvider`, y su
constructor (via `AzureMonitorTransmitter.InitializeConnectionVars`) lanza
`InvalidOperationException("A connection string was not found...")` si no hay connection string
resoluble -- verificado empiricamente con un proyecto de consola aislado que reproduce el wiring
exacto del proyecto (incluyendo `UseFunctionsWorkerDefaults()`). El `AddView` de arriba filtra
MEDICIONES, no evita esta construccion: el reader se arma igual, con o sin vistas.

Sin manejo adicional, esto significa que un arranque en frio con la Key Vault reference de
`APPLICATIONINSIGHTS_CONNECTION_STRING` aun sin resolver tumbaria el Function App entero al
resolver `MeterProvider` -- un modo de fallo mas severo que el que ya se acepta para trazas (que
"solo falla al exportar", actualizacion 2026-06-18 de este ADR) y que existia igual antes de este
cambio, solo que enmascarado porque nadie resolvia `MeterProvider` explicitamente hasta ahora.
Se agrega un `services.PostConfigure<AzureMonitorExporterOptions>(...)` que rellena
`ConnectionString` con un valor sintacticamente valido pero inerte
(`InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://dummy...`)
**solo si sigue vacio** tras todas las demas fuentes de configuracion (`PostConfigure` corre
siempre despues de `Configure` en el pipeline de `Microsoft.Extensions.Options`) -- si la variable
de entorno real esta presente, este relleno nunca se activa. Es seguro precisamente porque el
`AddView` de arriba droppea toda medicion: el reader nunca tiene nada que exportar, asi que nunca
abre una conexion HTTP real con ese valor inerte (verificado: `ForceFlush()` retorna en
milisegundos, sin intentos de red).

**Verificacion de CA-2 (guardrail de composicion).** El patron de `ConfiguracionObservabilidadProjectionsTests`
(issue #414, "lee las opciones/providers EFECTIVOS del contenedor, no la intencion del codigo") se
replica con `SupresionMetricasOTelTests` en cada uno de los tres dominios: compone el contenedor
real, agrega un `ConfigureOpenTelemetryMeterProvider` adicional con un `Meter` arbitrario (no
anticipado por el codigo de produccion) y un `InMemoryExporter`, y afirma que la coleccion
exportada queda vacia tras `ForceFlush()`. Verifica el efecto observable, no el mecanismo -- un
upgrade del exporter que reactive metricas por otra via haria fallar el test igual.

**Hallazgo de la revision: el seam pasa al overload con callback para desacoplar los logs de error
del sampler de trazas.** Al tocar el wiring de OTel de los tres dominios, la revision constato que
`EnableTraceBasedLogsSampler` seguia en su default (`true`) en las tres Function Apps -- MEF-ADR-0038
seccion 9 lo declara **mecanismo del marco, no opt-in**, extendido al write-side por harness #700, y
solo estaba aplicado en el worker de proyecciones (`ConfiguracionObservabilidadProjections`, issue
#414). Con ese default, el exporter instala `LogFilteringProcessor` en vez de un
`BatchLogRecordExportProcessor` plano, y ese processor descarta en silencio todo `LogRecord` emitido
dentro de un span que no quedo `Recorded`. En el write-side no hay ningun filtro estructural de spans
(la seccion 5 de ese ADR es exclusiva del worker): la perdida es directamente proporcional al ratio
de muestreo, y los tres dominios corren con `TELEMETRY_SAMPLING_RATIO` en su default **0.2** -- o sea
que ~80% de los `LogError` de handlers, Wolverine y Marten emitidos dentro de un span no muestreado
nunca llegaban a `exceptions`, que es justo la tabla que la alerta `exception_spike` (Capa 4) mira.
El seam pasa de `.UseAzureMonitorExporter()` a `.UseAzureMonitorExporter(o => o.EnableTraceBasedLogsSampler = false)`.

Ese cambio de overload tiene una consecuencia documentada en MEF-ADR-0038 seccion 9: la forma sin
argumentos registra `DefaultAzureMonitorExporterOptions` (que lee `APPLICATIONINSIGHTS_CONNECTION_STRING`
directo del entorno) y la forma con callback no, dejando la resolucion de la connection string
unicamente en manos de `Configure<IConfiguration>`. Verificado por ejecucion que ese camino sigue
resolviendola (el SDK de OpenTelemetry registra un `IConfiguration` con proveedor de variables de
entorno cuando no hay host detras, y `FunctionsApplication.CreateBuilder` lo incluye en produccion) --
MEF-ADR-0025 intacto: el seam sigue sin leer ni recibir el secreto.

**Guardrails agregados en la revision** (`ComposicionServiciosTests` de los tres dominios, mismo
patron de contenedor real):

1. `AgregarServicios{Dominio}_DeshabilitaElSamplerDeLogsBasadoEnTrazas` -- resuelve
   `IOptions<AzureMonitorExporterOptions>` del `ServiceProvider` real y afirma
   `EnableTraceBasedLogsSampler == false`. Verificada su no-vacuidad: rojo al revertir el flip,
   verde con el.
2. `AgregarServicios{Dominio}_ConservaLaConnectionStringReal_CuandoLaVariableDeEntornoEstaPresente` --
   cierra el riesgo que introduce el `PostConfigure` de arriba. Ese fallback es seguro **solo**
   mientras corra despues de la fuente real y no la pise; si algun dia la pisara, trazas y logs
   (que comparten `AzureMonitorExporterOptions`) se exportarian a un endpoint inexistente en
   silencio y de forma permanente -- un modo de fallo peor que el `InvalidOperationException` que el
   fallback vino a evitar, porque no se manifiesta como error sino como ausencia total de
   telemetria. El orden `Configure` -> `PostConfigure` es hoy correcto, pero nada en el codigo lo
   garantizaba frente a un upgrade del exporter: este test es esa garantia.

Capas 1 y 3 sin cambios (Capa 3 se mantiene en 0.5 GB, ver "Decision" arriba). La Capa 2 (sampling
de trazas) y su sampler no cambian: el recorte de metricas es exclusivamente del pipeline de
metricas, y el flip de `EnableTraceBasedLogsSampler` no altera la decision de muestreo de trazas --
solo deja de propagarla a los logs.

## Actualizacion (2026-08-30, issue #517): el worker de proyecciones conserva la familia `dotnet.gc.*`

Mismo diagnostico que el #515 (mismo episodio de `ingestion-warning`, mismo hallazgo de que
`UseAzureMonitorExporter()` activa el pipeline de metricas sin opt-out por senal), pero decision de
producto distinta para `Bitakora.ControlAsistencia.Projections`: **se recorta todo salvo la familia
GC**, no todo sin excepcion.

**Por que este proceso es la excepcion.** Las tres Function Apps del #515 son procesos efimeros que
escalan a cero; este worker corre 24/7 (`min_replicas = 1`, MEF-ADR-0034 seccion 8) hospedando el
daemon de proyecciones de los cuatro dominios. Un memory leak en una proyeccion es un riesgo real y
silencioso -- el unico dato que lo expone con antelacion es la serie de heap
(`dotnet.gc.last_collection.heap.size`, `dotnet.gc.last_collection.heap.fragmentation.size`,
`dotnet.gc.collections`), que a ~10-12k items/dia (~0.005 GB, ~1% del daily cap de 0.5 GB) es
estructuralmente barata frente al 23% que consumia la exportacion sin filtrar.

**Mecanismo: `AddView` func-based, no dos `AddView` por patron de nombre.** La primera aproximacion
considerada -- `AddView("dotnet.gc.*", MetricStreamConfiguration.Default)` seguido de
`AddView("*", MetricStreamConfiguration.Drop)` -- se descarto tras leer la xmldoc de
`MeterProviderBuilderExtensions.AddView` embebida en el paquete `OpenTelemetry` 1.16.0: "las vistas
se aplican en el orden en que se agregan", que en la especificacion de OpenTelemetry Metrics
significa que un instrumento que matchea **varias** vistas produce un `MetricStream` **por cada
una** (fan-out), no que "la primera vista que matchea gana". Con las dos vistas name-based, un
instrumento `dotnet.gc.*` calzaria con ambos patrones y el wildcard `"*"` seguiria generando un
segundo stream dropeado para el mismo instrumento -- ambiguo y dependiente de un detalle de
implementacion no verificado (si ese segundo stream "dropeado" convive sin efecto con el primero, o
si interfiere).

Se usa en cambio el overload `AddView(Func<Instrument, MetricStreamConfiguration>)`, que registra
una **unica** vista que decide por instrumento (`ConfiguracionObservabilidadProjections.SuprimirSalvoFamiliaGC`):
para un instrumento cuyo nombre empieza por `dotnet.gc.` devuelve `null`, y para cualquier otro
devuelve `MetricStreamConfiguration.Drop`. La misma xmldoc documenta el efecto de `null`: "una
`MetricStreamConfiguration` invalida devuelta por `viewConfig` causara que la vista se ignore para
ese instrumento, sin error en runtime" -- es decir, sin ninguna vista aplicable el instrumento cae al
agregado por defecto (se conserva sin intervencion), exactamente como si el `AddView` no existiera
para el. Sin fan-out, sin dos registros que puedan divergir, y sin necesidad de descompilar el
exporter (a diferencia del hallazgo del reader sincronico del #515, este mecanismo esta documentado
en la xmldoc publica del paquete -- gate del planner resuelto sin necesitar MCP de docs).

**Mismo hallazgo del #515 sobre el reader sincronico, mismo mitigante.** El reader de metricas de
Azure Monitor exige connection string de forma sincronica al resolver `MeterProvider`
(`InvalidOperationException` si falta), sin importar que las vistas droppeen casi todo despues. Se
replica el mismo `services.PostConfigure<AzureMonitorExporterOptions>(...)` que rellena
`ConnectionString` con el valor sintactico inerte del #515 **solo si sigue vacio** tras las demas
fuentes de configuracion -- inerte tambien aqui porque, salvo la familia GC (volumen minimo), el
resto sigue dropeado y el reader no abre conexion real por los instrumentos suprimidos.

**Verificacion de CA-2 (guardrail de composicion).** Mismo patron que `SupresionMetricasOTelTests`
del #515 (contenedor real + `ConfigureOpenTelemetryMeterProvider` adicional con un `Meter` y un
`InMemoryExporter` propios), pero con una asercion mas fina: un instrumento arbitrario y uno de la
familia GC emitidos **en la misma composicion** -- el arbitrario se suprime, el GC sobrevive. Un test
que solo emitiera el instrumento GC (sin el arbitrario) pasaria en verde incluso con una
implementacion que no suprimiera nada; la combinacion es lo que demuestra que el recorte es
selectivo, no binario.

**CA-4 (medicion post-deploy) queda pendiente, no resuelto por este cambio.** El gate que dejo el
planner -- si `_APPRESOURCEPREVIEW_` (latido propio del exporter OTel) es suprimible por `AddView`
o si sobrevive por viajar fuera del pipeline de vistas del proyecto -- no se pudo verificar sin un
Application Insights real: la xmldoc del paquete documenta el mecanismo de `AddView` en general, no
el instrumento especifico de ese latido. Si el residuo aparece en la medicion KQL de CA-4, este ADR
debe actualizarse con el hallazgo (mismo criterio que el hallazgo del reader sincronico en el #515:
un mecanismo documentado puede tener excepciones no documentadas en la practica).

Capas 1 y 3 sin cambios (Capa 3 se mantiene en 0.5 GB). La Capa 2 (sampling de trazas) no cambia: el
recorte de metricas es exclusivamente del pipeline de metricas del worker, que ya tenia su propio
tracing configurado (issues #308/#414) sin tocar.
