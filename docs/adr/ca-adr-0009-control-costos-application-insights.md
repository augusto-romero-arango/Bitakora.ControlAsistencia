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
| `<prefijo>-servicebus-failure-spike` | Consumidores de eventos | invocaciones con `success == false` cuyo `resultCode` no es un status HTTP |

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
