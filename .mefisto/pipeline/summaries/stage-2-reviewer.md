# Stage 2 - Reviewer (issue #994)

## Resultado
Los cambios del writer cubren CA-1, CA-2, CA-3, CA-4 y CA-6: workflow semanal + `workflow_dispatch` con `secrets.POSTGRES_CONNECTION_STRING`, SQL por schema (`programacion`, `control_horas`, `colaboradores`, `sedes` -- coinciden con `SchemaDelDominio` del worker de proyecciones), una transaccion por schema, limite `max(seq_id) < min(last_seq_id)`, antiguedad > 24 h, orden `mt_events` -> `mt_streams`, `mt_doc_*` por `mt_last_modified`, summary con filas y limite, y CA-ADR-0040 (decision 7) mas la fila de `CLAUDE.md` actualizados. No se tocaron rutas prohibidas.

Habia un bug bloqueante: la guarda de `TRUNCATE` hacia `grep -i truncate` sobre el archivo SQL completo, y el comentario "Nunca TRUNCATE" del encabezado la disparaba siempre. El job habria abortado en cada ejecucion.

CA-5 (evidencia en dev tras un `workflow_dispatch`, sin aviso de high water en App Insights y con el smoke de Programacion pasando) queda pendiente: requiere ejecutarlo en dev despues del merge.

## Correcciones
- `scripts/limpiar-tenants-smoke.sh`:
  - La guarda estatica ahora evalua el SQL sin comentarios `--`. Tambien exige al menos un `DELETE FROM`, no distingue mayusculas y tolera espacios variables.
  - La salida de `psql` (NOTICE y errores) se ve en el log del job. Antes iba a `/dev/null` y un fallo quedaba sin diagnostico.
  - El parseo de la cadena Npgsql recorta los espacios alrededor de las claves (p. ej. `; Database=`).
- `scripts/limpiar-tenants-smoke.sql`:
  - Nueva guarda en tiempo de ejecucion: si la tabla temporal `elegibles` contiene un tenant fuera de `tenant-smoke*`, aborta con `RAISE EXCEPTION`.
  - `set_config` se ejecuta con `\gset` para que no imprima una fila suelta en la salida.

## Verificacion
- `bash -n` sobre el script: OK.
- Script ejecutado con un `psql` simulado: variables PG* bien derivadas (incluye un password con `=`), summary en `$GITHUB_STEP_SUMMARY` con tablas de limite y filas.
- Guardas negativas: un `DELETE` sin filtro -> rc=1; un `TRUNCATE` real -> rc=1; el SQL real pasa.
- SQL ejecutado contra un Postgres local efimero (`initdb`/`pg_ctl`) con tablas tipo Marten conjoined:
  - Solo se borraron los streams de `tenant-smoke`/`tenant-smoke-*` con antiguedad > 24 h y `max(seq_id) < 50` (el min de la progresion).
  - Se conservaron: el stream con `max = 50` (limite estricto), el stream reciente, `tenant-real` y `tenant-smokex`.
  - En `mt_doc_vista` solo se borro el documento smoke viejo.
  - Un schema sin tablas -> `OMITIDO`; un schema no permitido (`public`) -> error.
- No hay codigo C# en el diff; no aplica `dotnet build`/`test`.
