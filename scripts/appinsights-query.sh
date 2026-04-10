#!/usr/bin/env bash
# appinsights-query.sh - Consultas KQL predefinidas contra Application Insights
#
# Uso:
#   ./scripts/appinsights-query.sh exceptions
#   ./scripts/appinsights-query.sh dead-letters
#   ./scripts/appinsights-query.sh function-errors
#   ./scripts/appinsights-query.sh traces --filter "NullReferenceException"
#   ./scripts/appinsights-query.sh health-summary
#   ./scripts/appinsights-query.sh exceptions --hours 48
#
# Requiere: az cli con sesion activa (az login)
# Configuracion: scripts/.env (ver scripts/.env.template)

set -euo pipefail

# --- Colores ----------------------------------------------------------------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# --- Logging -----------------------------------------------------------------
log()     { echo -e "${BLUE}[$(date +%H:%M:%S)]${NC} $1"; }
success() { echo -e "${GREEN}${BOLD}ok${NC} $1"; }
warn()    { echo -e "${YELLOW}!${NC} $1"; }
error()   { echo -e "${RED}${BOLD}ERROR:${NC} $1" >&2; }

# --- Configuracion -----------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"

if [ -f "$ENV_FILE" ]; then
    # shellcheck source=/dev/null
    source "$ENV_FILE"
else
    error "No se encontro $ENV_FILE"
    echo "  Copia el template y completa los valores:"
    echo "    cp scripts/.env.template scripts/.env"
    exit 1
fi

if [ -z "${APPINSIGHTS_APP:-}" ]; then
    error "Variable APPINSIGHTS_APP no definida en $ENV_FILE"
    exit 1
fi

if [ -z "${APPINSIGHTS_RG:-}" ]; then
    error "Variable APPINSIGHTS_RG no definida en $ENV_FILE"
    exit 1
fi

# --- Verificar az login ------------------------------------------------------
if ! az account show &>/dev/null; then
    error "No hay sesion activa de Azure CLI. Ejecuta 'az login' primero."
    exit 1
fi

# --- Parametros --------------------------------------------------------------
COMMAND="${1:-}"
HOURS=24
FILTER=""
MAX_ROWS=50

if [ -z "$COMMAND" ]; then
    echo -e "${CYAN}${BOLD}appinsights-query.sh${NC} - Consultas KQL contra App Insights"
    echo ""
    echo "Comandos disponibles:"
    echo "  exceptions       Top 20 excepciones agrupadas por tipo y mensaje"
    echo "  dead-letters     Mensajes dead-lettered en traces"
    echo "  function-errors  Funciones con requests fallidas"
    echo "  traces           Traces (usar con --filter para filtrar)"
    echo "  health-summary   Vista rapida: excepciones + requests fallidas + disponibilidad"
    echo ""
    echo "Opciones:"
    echo "  --hours N        Ventana temporal en horas (default: 24)"
    echo "  --filter TEXT    Filtrar por texto (solo para 'traces')"
    exit 1
fi

shift

while [[ $# -gt 0 ]]; do
    case "$1" in
        --hours)
            HOURS="$2"
            shift 2
            ;;
        --filter)
            FILTER="$2"
            shift 2
            ;;
        *)
            error "Opcion desconocida: $1"
            exit 1
            ;;
    esac
done

# --- Ejecutar query ----------------------------------------------------------
run_query() {
    local query="$1"
    local description="$2"

    log "$description (ultimas ${HOURS}h)"

    local result
    if ! result=$(az monitor app-insights query \
        --app "$APPINSIGHTS_APP" \
        --resource-group "$APPINSIGHTS_RG" \
        --analytics-query "$query" \
        --output table 2>&1); then
        error "Fallo la consulta KQL"
        echo "$result" >&2
        exit 1
    fi

    echo "$result"
    success "Consulta completada"
}

# --- Queries KQL predefinidas ------------------------------------------------
case "$COMMAND" in
    exceptions)
        run_query \
            "exceptions | where timestamp > ago(${HOURS}h) | summarize count() by type, outerMessage | order by count_ desc | take 20" \
            "Top 20 excepciones agrupadas por tipo y mensaje"
        ;;

    dead-letters)
        run_query \
            "traces | where timestamp > ago(${HOURS}h) | where message has 'dead' or message has 'deadletter' or message has 'Dead' | project timestamp, message, operation_Name | order by timestamp desc | take ${MAX_ROWS}" \
            "Mensajes dead-lettered en traces"
        ;;

    function-errors)
        run_query \
            "requests | where timestamp > ago(${HOURS}h) | where success == false | summarize failedCount=count() by name, resultCode | order by failedCount desc | take ${MAX_ROWS}" \
            "Funciones con requests fallidas"
        ;;

    traces)
        if [ -n "$FILTER" ]; then
            run_query \
                "traces | where timestamp > ago(${HOURS}h) | where message has '${FILTER}' | project timestamp, message, severityLevel, operation_Name | order by timestamp desc | take ${MAX_ROWS}" \
                "Traces filtradas por '${FILTER}'"
        else
            run_query \
                "traces | where timestamp > ago(${HOURS}h) | project timestamp, message, severityLevel, operation_Name | order by timestamp desc | take ${MAX_ROWS}" \
                "Todas las traces"
        fi
        ;;

    health-summary)
        log "Health summary (ultimas ${HOURS}h)"

        echo -e "\n${CYAN}${BOLD}--- Excepciones ---${NC}"
        az monitor app-insights query \
            --app "$APPINSIGHTS_APP" \
            --resource-group "$APPINSIGHTS_RG" \
            --analytics-query "exceptions | where timestamp > ago(${HOURS}h) | summarize totalExceptions=count(), distinctTypes=dcount(type) | project totalExceptions, distinctTypes" \
            --output table 2>&1 || true

        echo -e "\n${CYAN}${BOLD}--- Requests fallidas ---${NC}"
        az monitor app-insights query \
            --app "$APPINSIGHTS_APP" \
            --resource-group "$APPINSIGHTS_RG" \
            --analytics-query "requests | where timestamp > ago(${HOURS}h) | summarize totalRequests=count(), failedRequests=countif(success == false), availabilityPct=round(100.0 * countif(success == true) / count(), 2)" \
            --output table 2>&1 || true

        echo -e "\n${CYAN}${BOLD}--- Top 5 errores ---${NC}"
        az monitor app-insights query \
            --app "$APPINSIGHTS_APP" \
            --resource-group "$APPINSIGHTS_RG" \
            --analytics-query "exceptions | where timestamp > ago(${HOURS}h) | summarize count() by type | order by count_ desc | take 5" \
            --output table 2>&1 || true

        success "Health summary completado"
        ;;

    *)
        error "Comando desconocido: $COMMAND"
        echo "Ejecuta sin argumentos para ver los comandos disponibles."
        exit 1
        ;;
esac
