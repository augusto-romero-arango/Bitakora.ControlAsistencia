# ADR-0009: Control de costos de Application Insights

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
| `maxTelemetryItemsPerSecond` | 5 | 5 | 10 |
| Exception spike threshold | 50 | 100 | 200 |
