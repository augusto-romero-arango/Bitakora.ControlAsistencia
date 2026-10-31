# Rendimiento: benchmarks y SLI

Hogar de los benchmarks y SLI de ControlAsistencias contra dev ([CA-ADR-0044](../../docs/adr/ca-adr-0044-proyecto-de-rendimiento.md)).
CI lo compila pero no lo ejecuta: no termina en `.Tests`. Corre a mano, en local o con el workflow
[`rendimiento.yml`](../../.github/workflows/rendimiento.yml).

> No lo corras mientras corren deploys o smoke tests en dev: carga el entorno y las mediciones se contaminan.

## Catálogo de SLI

| Benchmark (trait) | SLI | SLO | Origen |
|---|---|---|---|
| `programacion-masiva` | Desde la llamada a `solicitar_programacion_turno_por_grupo` hasta que `AsistenciaDiaria` tiene todos los días del mes de todo el grupo (100 colaboradores x 1 mes) | ≤ 60 s | CA-ADR-0043, CA-ADR-0044 |

### `programacion-masiva`

1. **Foto inicial de infraestructura.**
2. **Preparación** (fuera del SLI), por las tools del MCP:
   - una sede `carga-{corrida}`;
   - un turno completo (08:00-16:00);
   - N colaboradores `carga-{corrida}-{NNNN}` en esa sede.

   La preparación espera a que el grupo sea visible en `listar_colaboradores`.
3. **Inicio del SLI:** `solicitar_programacion_turno_por_grupo` por la sede, para el mes completo.
4. **Fin del SLI:** conteo de solo lectura en `control_horas.mt_doc_asistenciadiaria` (tenant `tenant-smoke`, `id LIKE 'dc:carga-{corrida}-%'`) cada 5 s, desde el inicio del cronómetro, hasta N x días del mes o el tiempo máximo.
5. **Fases:** cuándo ocurrió cada eslabón de la cadena, leído de los timestamps de Postgres (solo lectura).
6. **Métricas de Azure Monitor** de la ventana y **foto final.** La ventana va de un minuto antes del inicio a cuatro después del fin: Azure Monitor fecha la actividad hasta dos minutos después del reloj del runner, y una ventana más corta corta la cola del procesamiento.
7. **Reporte y aserción del SLO.**

El MCP programa siempre por el camino en lote (`POST programacion/solicitudes-en-fechas`, CA-ADR-0043): el camino no es un parámetro del benchmark.

## Reporte

Cada corrida escribe `{benchmark}-{corrida}.md` y `.json` en `Rendimiento:DirectorioReportes` (por defecto `bin/.../reportes/`). En el workflow, el markdown va además al summary del job y los dos archivos al artifact `rendimiento-*`.

- **SLI contra SLO**, parámetros, resultados y hallazgos (fallidos, filas faltantes).
- **Progreso** del conteo.
- **Fases:** para cada eslabón, cantidad y segundos desde el inicio del SLI del primero, la mediana, el p90 y el último:
  - `programacion_turno_solicitada` en `programacion.mt_events`: cuándo confirma cada POST de la tool;
  - `turno_diario_asignado` en los streams `cd:` de `control_horas.mt_events`: el primer salto en ControlHoras;
  - `depuracion_dia_recibida` en los streams `dc:`: el segundo salto;
  - `mt_last_modified` de `AsistenciaDiaria`: la proyección.

  El origen es el reloj de la base tomado justo antes del cronómetro, así que no depende del reloj del runner. Tampoco depende de App Insights, que deja de recibir telemetría cuando llega a su tope diario. Si la consulta falla, la corrida igual deja su reporte y lo anota en hallazgos.
- **Infraestructura de la corrida:** huella inicial y final, y los valores capturados:
  - SKU y parámetros de Postgres;
  - configuración de Service Bus;
  - planes y Function Apps, con su SHA desplegado y el `host.json` de ese commit;
  - el worker de proyecciones.
- **Métricas de Azure Monitor** minuto a minuto, que es la granularidad mínima de las métricas de plataforma:
  - CPU y memoria de los planes de ControlHoras, Programación y MCP;
  - CPU, créditos, conexiones, memoria, IOPS, cola de disco y porcentaje de IOPS consumido de Postgres;
  - CPU y memoria de proyecciones;
  - mensajes activos (máximo por minuto), entrantes y completados (total por minuto) por entidad de Service Bus.
- **Muestreo propio cada 5 s:** el progreso del conteo lleva además las conexiones a la base por estado (`pg_stat_activity`: total, activas, inactivas, en transacción), con más resolución que la métrica de plataforma. Incluye la conexión del propio muestreo.

### Análisis de sensibilidad

1. Corre el benchmark y guarda su JSON.
2. Cambia **un solo** recurso (p. ej. `maxConcurrentCalls` en el `host.json` de ControlHoras, el SKU de Postgres o `lockDuration` de la cola).
3. Vuelve a correr y compara los dos JSON: `infraestructura.inicial.valores` muestra qué cambió y la huella lo confirma. El SLI y las métricas muestran el efecto.

Para medir un recurso o campo nuevo, agrégalo a la sección `Infraestructura` de `appsettings.json` (`Recursos` con su `ApiVersion` y `Campos`, o `AppSettingsPermitidos`). Nunca agregues un app setting con secretos a la allowlist: el reporte termina en un artifact.

## Cómo correrlo

### Con el workflow

Actions → **Rendimiento** → *Run workflow* desde `main`. La credencial federada solo acepta `main`. Inputs: benchmark, colaboradores, mes (`yyyy-MM`, vacío = el siguiente), SLO y tiempo máximo.

### En local

Requisitos:

- **Firewall:** tu IP en el firewall de `psql-asist-dev`.
- **Azure:** `az login` en la suscripción de dev, para la foto y las métricas. Sin credencial el benchmark corre igual y el reporte las marca como no disponibles.
- **`appsettings.local.json`** en esta carpeta (está ignorado por Git):

```json
{
  "Mcp": { "FunctionsKey": "<az functionapp keys list -g rg-controlasistencias-dev -n func-asist-dev-mcp-asistente --query systemKeys.mcp_extension -o tsv>" },
  "Postgres": { "ConnectionString": "<la misma de los smoke de ControlHoras>" }
}
```

```bash
dotnet test --project tests/Bitakora.ControlAsistencia.Rendimiento --configuration Release \
  --filter-trait "Benchmark=programacion-masiva"
```

Los parámetros se sobreescriben por variables de entorno, p. ej. `Rendimiento__ProgramacionMasiva__Colaboradores=2` para una corrida corta de humo, o `Rendimiento__RetrasoMetricasMinutos=0` si no necesitas las métricas.

Los datos sembrados quedan en `tenant-smoke` y los borra la limpieza semanal ([CA-ADR-0040](../../docs/adr/ca-adr-0040-eliminacion-de-datos-de-smoke-fragiles.md)).

## Cómo agregar un benchmark

1. Una clase en una carpeta por dominio (como `Programacion/`) con un `[Fact]` y `[Trait("Benchmark", "<nombre>")]`. Las fixtures (`McpFixture`, `PostgresFixture`, `AzureFixture`) entran como `IClassFixture`.
2. Parámetros en `appsettings.json`, bajo `Rendimiento:<Benchmark>`, sobreescribibles por entorno.
3. Un `ReporteDeCorrida`: la foto inicial y la final, `Sli` y las métricas de la ventana. `EscribirAsync` va **antes** de la aserción del SLO.
4. Agrega el trait a las opciones del input `benchmark` de `rendimiento.yml` y una fila al catálogo de arriba.
