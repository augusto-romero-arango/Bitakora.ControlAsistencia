#!/usr/bin/env bash
# Limpieza semanal de tenants de smoke (issue #994). Requiere POSTGRES_CONNECTION_STRING (formato Npgsql).
# Escribe el resumen en $GITHUB_STEP_SUMMARY si existe.
set -euo pipefail

: "${POSTGRES_CONNECTION_STRING:?POSTGRES_CONNECTION_STRING no definido}"
dir="$(cd "$(dirname "$0")" && pwd)"
sql="$dir/limpiar-tenants-smoke.sql"

# Guarda (CA-2): toda sentencia DELETE del script debe filtrar por tenant; sin TRUNCATE.
if grep -qiE 'truncate' "$sql"; then echo "::error::El script contiene TRUNCATE"; exit 1; fi
sin_filtro=$(awk 'BEGIN{RS=";"} /DELETE FROM/ && !/tenant-smoke/ {c++} END{print c+0}' "$sql")
if [ "$sin_filtro" -ne 0 ]; then echo "::error::DELETE sin filtro de tenant-smoke"; exit 1; fi

# Convertir cadena Npgsql (Host=..;Database=..;Username=..;Password=..) a variables PG*.
get() { printf '%s' "$POSTGRES_CONNECTION_STRING" | tr ';' '\n' | awk -F= -v k="$1" 'tolower($1)==tolower(k){sub(/^[^=]*=/,""); print; exit}'; }
PGHOST="$(get Host)"; [ -n "$PGHOST" ] || PGHOST="$(get Server)"
PGPORT="$(get Port)"; [ -n "$PGPORT" ] || PGPORT=5432
PGDATABASE="$(get Database)"
PGUSER="$(get Username)"; [ -n "$PGUSER" ] || PGUSER="$(get 'User Id')"
PGPASSWORD="$(get Password)"
export PGHOST PGPORT PGDATABASE PGUSER PGPASSWORD
export PGSSLMODE=require
echo "::add-mask::$PGPASSWORD"

salida="$(mktemp)"
trap 'rm -f "$salida"' EXIT
for schema in programacion control_horas colaboradores sedes; do
  echo "== Schema $schema =="
  psql -v ON_ERROR_STOP=1 -v schema="$schema" -X -q -f "$sql" 2>&1 | tee -a "$salida" >/dev/null
done

{
  echo "## Limpieza de tenants de smoke"
  echo
  echo "| Schema | Limite seq_id (min last_seq_id) |"
  echo "|---|---|"
  grep -o 'LIMITE|[^|]*|[0-9]*' "$salida" | awk -F'|' '{print "| "$2" | "$3" |"}' || true
  grep -o 'OMITIDO|.*' "$salida" | awk -F'|' '{print "| "$2" | omitido: "$3" |"}' || true
  echo
  echo "| Schema | Tabla | Filas borradas |"
  echo "|---|---|---|"
  grep -o 'RESULT|[^|]*|[^|]*|[0-9]*' "$salida" | awk -F'|' '{print "| "$2" | "$3" | "$4" |"}' || true
} | tee -a "${GITHUB_STEP_SUMMARY:-/dev/null}"
