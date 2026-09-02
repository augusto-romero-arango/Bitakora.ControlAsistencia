---
fecha: 2026-08-31
hora: 01:15
sesion: verificacion-supervisada
tema: falla inducida del issue #413 -- alerta background-exception-spike (#415) y mediciones delegadas por el #414
---

## Contexto
Issue #413 declaraba explicitamente que su entregable era documental y su ejecucion una sesion
supervisada por humano, no un pipeline de Mefisto (`/infra` fallo al intentarlo: "No hay commits en
la rama", esperado -- el issue no toca HCL de entrada). El usuario pidio ejecutar directamente el
protocolo de falla inducida del #412 contra la alerta generalizada del #415, cerrando ademas las dos
mediciones que el #414 delego aqui.

## Ejecucion
- `az postgres flexible-server stop` sobre `psql-asist-dev`: emitido 01:15:12Z, `Stopped` 01:20:05Z.
- Caida sostenida 21 minutos (Monitor con chequeos cada ~5 min contra Application Insights).
- `az postgres flexible-server start`: emitido 01:39:34Z, `Ready` 01:41:41Z.
- Mediciones via `az monitor app-insights query` (appId `dbeb3cea-a2db-4173-adfb-606c77a15301`),
  `az monitor log-analytics query` (workspace `controlasistencias-dev-logs`) y
  `az rest` contra `Microsoft.AlertsManagement/alerts`.

## Descubrimientos
- La alerta `background-exception-spike` disparo 4 veces (una por `cloud_RoleName`) a los 5m47s del
  stop -- deteccion confirmada, el hueco que motivo el #415 queda cerrado.
- Las tres Function Apps saturan en 120 excepciones/ventana durante caida total; el worker llega a
  54, por encima del estimado del #415 (35-42) pero con MAS margen sobre el umbral (3.6x vs
  2.3-2.8x proyectado) -- el estimado fue conservador a la baja, no compromete el umbral.
- El desacople `EnableTraceBasedLogsSampler=false` del #414 cierra el 0% de ratio medido en el #412:
  ahora 196/196 (1:1) para la familia `HighWaterAgent`.
- El volumen de la falla (196 excepciones extra en 21 min) no mueve la aguja del daily cap: la hora
  de la caida ingesto 0.051 GB, en linea con horas vecinas sin incidente.
- Reversion limpia: Postgres Ready, worker sin re-arrancar (replica creada antes del stop), 0
  excepciones residuales, 5/5 Function Apps Running, dependencias Postgres 100% exitosas.

## Decisiones
1. Umbral de `background-exception-spike` (>15, persistencia 2/2) se confirma sin ajuste -- no se
   crea issue de infra ni se toca `infra/modules/monitoring/main.tf`.
2. Resultados documentados en seccion fechada 2026-08-31 de `docs/adr/ca-adr-0009-control-costos-application-insights.md`.
3. Los seis criterios de aceptacion del issue #413 quedan cumplidos; se cierra por PR de docs (no
   pipeline `/infra`, consistente con lo que el propio issue declaraba).

## Nota operativa (advertencia al agente)
Durante la sesion, `ScheduleWakeup` con un prompt que empaquetaba todo el protocolo (parar/arrancar
Postgres + mediciones + edicion de ADR + PR) fue bloqueado por el clasificador de permisos de modo
automatico -- correctamente: es una accion de alto impacto que debe quedar en el turno activo,
supervisada, no delegada a un wakeup autonomo. La ejecucion se hizo con Bash directo (permitido) y
`Monitor` con un loop acotado en el tiempo para sostener la espera sin bloquear la conversacion.
