# CA-ADR-0044: Proyecto de rendimiento para benchmarks y SLI

## Estado

Aceptado (2026-10-31). Origen: #1335 (prueba de carga de programacion masiva). Complementa CA-ADR-0041 y
actualiza la meta de CA-ADR-0043.

## Contexto

CA-ADR-0043 fijo una meta de throughput para la programacion masiva y dejo dos caminos (por dia y en fechas)
que hay que comparar con mediciones. CA-ADR-0041 saca el volumen de los smoke tests, asi que la medicion
necesita un hogar propio. #1335 la planteo como consola en `tools/`, pero el pipeline de tooling de Mefisto no
puede escribir ni `tools/` ni el `.slnx` (alcance de `tooling-pipeline.sh`), y una herramienta suelta no deja
lugar para los siguientes benchmarks.

Ademas, una medicion de rendimiento solo se puede comparar con otra si se sabe sobre que infraestructura
corrio: SKU de Postgres y sus parametros, configuracion de Service Bus, `host.json` desplegado de cada Function
App, recursos del worker de proyecciones. Sin eso no se puede atribuir una mejora a un cambio concreto.

## Decision

1. **Un solo proyecto.** `tests/Bitakora.ControlAsistencia.Rendimiento` es el hogar de todos los benchmarks y
   SLI del BC. Es un proyecto xUnit v3 (mismo patron de fixtures que los SmokeTests); no lleva el sufijo
   `.Tests`, asi que el glob de `ci.yml` no lo ejecuta. Esta en el `.slnx`, asi que CI lo compila.
2. **Un benchmark es un test.** Lleva `[Trait("Benchmark", "<nombre>")]` y su SLO es la asercion. El reporte
   (markdown y JSON) se escribe **antes** de la asercion, para que una corrida que no cumple tambien deje su
   evidencia.
3. **Foto de infraestructura.** Cada corrida captura, al inicio y al final, la configuracion de los recursos
   que afectan la medicion (seccion `Infraestructura` de `appsettings.json`, ampliable sin tocar codigo) y
   calcula su huella. Si la foto final difiere de la inicial, el reporte lo advierte. Comparar el JSON de dos
   corridas aisla el efecto de un cambio individual (analisis de sensibilidad).
4. **Sin secretos en el reporte.** La foto solo toma campos declarados y app settings de una allowlist. Nunca se
   vuelcan app settings completos ni connection strings, porque el reporte termina en un artifact.
5. **Fuente del tiempo y del fin.** El reloj del cliente mide el SLI. El fin se detecta con un conteo de solo
   lectura en Postgres (CA-ADR-0040), no con App Insights, que muestrea (CA-ADR-0009). Las metricas de
   plataforma de Azure Monitor (sin muestreo) acompanan el reporte, minuto a minuto.
6. **Ejecucion manual.** El workflow `rendimiento.yml` solo se dispara a mano (`workflow_dispatch`, desde
   `main` por la credencial federada) y se une al grupo de concurrencia `smoke-tests-dev`, para no solaparse
   con los smoke. Nunca corre desde CI ni desde un deploy.
7. **Datos.** Los benchmarks siembran en `tenant-smoke` (la identidad interina del MCP sin Bearer) con codigos
   propios por corrida; la limpieza semanal los borra (CA-ADR-0040). Postgres es de solo lectura para el
   benchmark.
8. **Primer SLI: programacion masiva.** Desde la llamada a `solicitar_programacion_turno_por_grupo` hasta que
   `AsistenciaDiaria` tiene todos los dias del mes de todo el grupo: **100 colaboradores x 1 mes en <= 60 s**.
   Reemplaza la meta de 130 colaboradores de CA-ADR-0043.
9. **Construccion fuera de los pipelines de Mefisto.** El proyecto se construye y evoluciona directamente,
   porque el alcance de tooling no cubre proyectos nuevos ni el `.slnx`.

## Alternativas consideradas

- **Consola en `tools/` (#1335 original).** Descartada: el pipeline de tooling no la puede escribir y deja un
  proyecto por benchmark.
- **Benchmarks dentro de los SmokeTests.** Descartada por CA-ADR-0041: los smoke no siembran volumen y corren
  despues de cada deploy.
- **Corrida programada (cron).** Descartada por ahora: carga dev sin que nadie lo pida y compite con deploys y
  smoke. Se puede agregar cuando haya historia de SLI que valga seguir.
- **Medir el fin con App Insights.** Descartada: el muestreo subestima los conteos.

## Consecuencias

### Positivas

- Las decisiones de capacidad (#1341) y de camino (#1340, #1350) se toman con evidencia comparable.
- Agregar un benchmark es agregar un test con su trait y su SLO.
- La foto de infraestructura hace visible un cambio de configuracion a mitad de una corrida (p. ej. un deploy).

### Negativas

- El workflow debe dispararse desde `main`; una rama no puede probarlo antes del merge.
- Correr en local exige la IP del operador en el firewall de `psql-asist-dev`, `az login` y la key del MCP.
- Una corrida carga dev y siembra datos que viven hasta la limpieza semanal.

## Referencias

- Issues: #1335 (origen), #1340 y #1350 (interruptor de camino), #1341 (capacidad).
- CA-ADR-0009, CA-ADR-0040, CA-ADR-0041, CA-ADR-0043; MEF-ADR-0013, MEF-ADR-0048.

## Control de cambios

- 2026-10-31: creado (#1335).
