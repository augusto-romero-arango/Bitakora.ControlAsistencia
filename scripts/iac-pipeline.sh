#!/usr/bin/env bash
# iac-pipeline.sh -- Pipeline IaC automatizado para ControlAsistencias
#
# Uso:
#   ./scripts/iac-pipeline.sh 42 --env dev
#   ./scripts/iac-pipeline.sh 42 --env dev --auto-apply    # Omite confirmacion (solo dev)
#   ./scripts/iac-pipeline.sh 42 --env dev --skip-apply    # Solo write + review, crea PR
#   ./scripts/iac-pipeline.sh 42 --env dev --from-stage 2  # Retomar desde Stage 2
#
# Ciclo completo: Issue -> Write (HCL) -> Review (plan) -> Apply -> PR

set -euo pipefail

# --- Colores ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# --- Logging ---
PIPELINE_DIR=".claude/pipeline"
LOG_DIR="$PIPELINE_DIR/logs"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
LOG_FILE="$LOG_DIR/iac-pipeline-$TIMESTAMP.log"

# --- Tracking de estado ---
AGENT_WR_DUR="" AGENT_WR_RES="pending"
AGENT_RV_DUR="" AGENT_RV_RES="pending"
AGENT_AP_DUR="" AGENT_AP_RES="pending"
PIPELINE_ERROR=""
LAST_AGENT_DURATION=0
CURRENT_STAGE="setup"

_strip_ansi() { sed 's/\x1b\[[0-9;]*m//g'; }
_log_file()   { echo -e "$1" | _strip_ansi >> "${LOG_FILE_ABS:-$LOG_FILE}"; }

log()     { local m="${BLUE}[$(date +%H:%M:%S)]${NC} $1"; echo -e "$m"; _log_file "$m"; }
success() { local m="${GREEN}${BOLD}v${NC} $1"; echo -e "$m"; _log_file "$m"; }
warn()    { local m="${YELLOW}!${NC} $1"; echo -e "$m"; _log_file "$m"; }
header()  { local m="\n${CYAN}${BOLD}-- $1 --${NC}"; echo -e "$m"; _log_file "$m"; }
abort() {
    PIPELINE_ERROR="$(echo "$1" | sed 's/"/\\"/g' | tr '\n' ' ')"
    echo -e "\n${RED}${BOLD}x ERROR: $1${NC}" | tee -a "${LOG_FILE_ABS:-$LOG_FILE}"
    echo -e "${YELLOW}Revisa el log: ${LOG_FILE_ABS:-$LOG_FILE}${NC}"
    if [ -n "${PIPELINE_DIR_ABS:-}" ]; then
        update_status "$CURRENT_STAGE" "failed"
    fi
    exit 1
}

update_status() {
    local stage="$1" state="$2"
    CURRENT_STAGE="$stage"
    local wr_dur="null" rv_dur="null" ap_dur="null"
    [ -n "$AGENT_WR_DUR" ] && wr_dur="$AGENT_WR_DUR"
    [ -n "$AGENT_RV_DUR" ] && rv_dur="$AGENT_RV_DUR"
    [ -n "$AGENT_AP_DUR" ] && ap_dur="$AGENT_AP_DUR"
    local error_val="null"
    [ -n "$PIPELINE_ERROR" ] && error_val="\"$PIPELINE_ERROR\""
    cat > "$PIPELINE_DIR_ABS/infra-status.json" <<EOJSON
{
  "issue": "${ISSUE_NUM:-null}",
  "title": "$(echo "${ISSUE_TITLE:-}" | sed 's/"/\\"/g')",
  "environment": "${ENVIRONMENT:-?}",
  "started": "$TIMESTAMP",
  "stage": "$stage",
  "state": "$state",
  "updated": "$(date +%Y-%m-%dT%H:%M:%S)",
  "log": "${LOG_FILE_ABS:-$LOG_FILE}",
  "agents": {
    "infra-writer":   {"duration": $wr_dur, "result": "$AGENT_WR_RES"},
    "infra-reviewer": {"duration": $rv_dur, "result": "$AGENT_RV_RES"},
    "infra-applier":  {"duration": $ap_dur, "result": "$AGENT_AP_RES"}
  },
  "last_error": $error_val
}
EOJSON
}

# --- Parsear argumentos ---
ISSUE_NUM=""
ENVIRONMENT=""
FROM_STAGE=1
AUTO_APPLY=false
SKIP_APPLY=false

if [ $# -eq 0 ]; then
    echo "Uso: $0 <issue-num> --env <dev|staging|prod> [--auto-apply] [--skip-apply] [--from-stage N]"
    exit 1
fi

POSITIONAL_ARGS=()
while [[ $# -gt 0 ]]; do
    case "$1" in
        --env)
            [ $# -lt 2 ] && abort "Falta el nombre del ambiente"
            ENVIRONMENT="$2"
            shift 2
            ;;
        --from-stage)
            [ $# -lt 2 ] && abort "Falta el numero de stage"
            FROM_STAGE="$2"
            shift 2
            ;;
        --auto-apply)
            AUTO_APPLY=true
            shift
            ;;
        --skip-apply)
            SKIP_APPLY=true
            shift
            ;;
        [0-9]*)
            POSITIONAL_ARGS+=("$1")
            shift
            ;;
        *)
            abort "Argumento no reconocido: $1"
            ;;
    esac
done

if [ ${#POSITIONAL_ARGS[@]} -gt 0 ] && [ -z "$ISSUE_NUM" ]; then
    ISSUE_NUM="${POSITIONAL_ARGS[0]}"
fi

[ -z "$ISSUE_NUM" ] && abort "Falta el numero de issue"
[ -z "$ENVIRONMENT" ] && abort "Falta --env (dev|staging|prod)"

if ! [[ "$FROM_STAGE" =~ ^[1-3]$ ]]; then
    abort "--from-stage debe ser 1, 2, o 3"
fi

# Proteccion: --auto-apply solo en dev
if [ "$AUTO_APPLY" = true ] && [ "$ENVIRONMENT" != "dev" ]; then
    abort "--auto-apply solo esta permitido en el ambiente 'dev'. En '$ENVIRONMENT' se requiere confirmacion manual."
fi

# Verificar que el directorio del ambiente existe
INFRA_ENV_DIR="infra/environments/$ENVIRONMENT"
[ -d "$INFRA_ENV_DIR" ] || abort "No existe el directorio de ambiente: $INFRA_ENV_DIR"

# --- Verificar dependencias ---
for cmd in claude gh git terraform; do
    command -v "$cmd" &>/dev/null || abort "Falta comando requerido: $cmd"
done

# --- Preparar directorio de pipeline ---
mkdir -p "$LOG_DIR"
echo "Pipeline IaC iniciado: $TIMESTAMP" > "$LOG_FILE"

PIPELINE_DIR_ABS="$(realpath "$PIPELINE_DIR")"
LOG_DIR_ABS="$(realpath "$LOG_DIR")"
LOG_FILE_ABS="$(realpath "$LOG_FILE")"
EVENTS_LOG_ABS="$PIPELINE_DIR_ABS/events.log"
INFRA_ENV_DIR_ABS="$(realpath "$INFRA_ENV_DIR")"

echo "=== SESSION IAC $TIMESTAMP issue:$ISSUE_NUM env:$ENVIRONMENT from-stage:$FROM_STAGE ===" >> "$EVENTS_LOG_ABS"

# --- Obtener issue ---
header "Preparando contexto"

log "Descargando issue #$ISSUE_NUM..."
ISSUE_JSON=$(gh issue view "$ISSUE_NUM" --json number,title,body,state 2>>"$LOG_FILE") \
    || abort "No se pudo obtener el issue #$ISSUE_NUM"
ISSUE_STATE=$(echo "$ISSUE_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)['state'])" 2>/dev/null || echo "UNKNOWN")
if [ "$ISSUE_STATE" != "OPEN" ]; then
    abort "El issue #$ISSUE_NUM esta $ISSUE_STATE -- solo se procesan issues abiertos."
fi
ISSUE_TITLE=$(echo "$ISSUE_JSON" | grep -o '"title":"[^"]*"' | sed 's/"title":"//;s/"//')
ISSUE_BODY=$(echo "$ISSUE_JSON" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['body'])" 2>/dev/null \
    || echo "$ISSUE_JSON" | sed 's/.*"body":"//;s/","[^"]*":".*//;s/\\n/\n/g;s/\\r//g')
ISSUE_CONTEXT="# Issue #$ISSUE_NUM: $ISSUE_TITLE

$ISSUE_BODY"
log "Issue: $ISSUE_TITLE"

echo "$ISSUE_CONTEXT" > "$PIPELINE_DIR/infra-input.md"

# --- Preparar rama de trabajo ---
header "Preparando rama"

SLUG=$(echo "$ISSUE_TITLE" | tr '[:upper:]' '[:lower:]' | tr ' ' '-' | sed 's/[^a-z0-9-]//g' | tr -s '-' | cut -c1-40 | sed 's/-$//')
BRANCH_NAME="infra-issue-${ISSUE_NUM}-${SLUG}"

CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)

if [ "$FROM_STAGE" -gt 1 ]; then
    log "Retomando desde Stage $FROM_STAGE en rama actual: $CURRENT_BRANCH"
else
    if git show-ref --verify --quiet "refs/heads/$BRANCH_NAME" 2>/dev/null; then
        log "Rama $BRANCH_NAME ya existe, cambiando a ella..."
        git checkout "$BRANCH_NAME" >>"$LOG_FILE" 2>&1
    else
        git pull origin "${CURRENT_BRANCH}" >>"$LOG_FILE" 2>&1 || warn "No se pudo hacer pull (continuando)"
        log "Creando rama: $BRANCH_NAME"
        git checkout -b "$BRANCH_NAME" >>"$LOG_FILE" 2>&1 \
            || abort "No se pudo crear la rama $BRANCH_NAME"
    fi
    success "Rama lista: $BRANCH_NAME"
fi

update_status "setup" "running"

# --- Funcion auxiliar para invocar agentes ---
run_agent() {
    local stage="$1"
    local agent="$2"
    local prompt="$3"
    local log_stage="$LOG_DIR_ABS/iac-stage-${stage}-${agent}-${TIMESTAMP}.log"
    local start_ts
    start_ts=$(date +%s)

    echo "[$(date +%H:%M:%S)] === IAC STAGE $stage: $agent ===" >> "$EVENTS_LOG_ABS"
    case "$agent" in
        infra-writer)   AGENT_WR_RES="running" ;;
        infra-reviewer) AGENT_RV_RES="running" ;;
        infra-applier)  AGENT_AP_RES="running" ;;
    esac
    update_status "$stage-$agent" "running"
    log "Invocando $agent..."

    local AGENT_TIMEOUT_SECONDS=1800
    claude -p "$prompt" \
        --agent "$agent" \
        --permission-mode bypassPermissions \
        --output-format text \
        >"$log_stage" 2>&1 &
    local CLAUDE_PID=$!
    (sleep $AGENT_TIMEOUT_SECONDS && kill $CLAUDE_PID 2>/dev/null && echo "[$(date +%H:%M:%S)] TIMEOUT: $agent supero ${AGENT_TIMEOUT_SECONDS}s" >> "$EVENTS_LOG_ABS") &
    local WATCHDOG_PID=$!
    wait $CLAUDE_PID || {
        kill $WATCHDOG_PID 2>/dev/null || true
        wait $WATCHDOG_PID 2>/dev/null || true
        local elapsed=$(( $(date +%s) - start_ts ))
        case "$agent" in
            infra-writer)   AGENT_WR_DUR=$elapsed; AGENT_WR_RES="failed" ;;
            infra-reviewer) AGENT_RV_DUR=$elapsed; AGENT_RV_RES="failed" ;;
            infra-applier)  AGENT_AP_DUR=$elapsed; AGENT_AP_RES="failed" ;;
        esac
        update_status "$stage-$agent" "failed"
        echo -e "\n${RED}-- Ultimas lineas del log de $agent:${NC}"
        tail -20 "$log_stage"
        abort "$agent fallo. Log completo: $log_stage"
    }

    kill $WATCHDOG_PID 2>/dev/null || true
    wait $WATCHDOG_PID 2>/dev/null || true
    local elapsed=$(( $(date +%s) - start_ts ))
    LAST_AGENT_DURATION=$elapsed
    log "$agent completado en ${elapsed}s"
}

# --- STAGE 1: infra-writer (escribir HCL) ---
if [ "$FROM_STAGE" -le 1 ]; then
    header "Stage 1: infra-writer (escribir HCL)"

    STAGE1_PROMPT="Estas en el directorio raiz del proyecto ControlAsistencias.

Contexto del issue de infraestructura a implementar:

$ISSUE_CONTEXT

Ambiente target: $ENVIRONMENT
Directorio del ambiente: $INFRA_ENV_DIR_ABS

Tu tarea: escribe o modifica los archivos Terraform necesarios para implementar este issue en el ambiente '$ENVIRONMENT'. Sigue todas las instrucciones de tu rol de infra-writer."

    run_agent "1" "infra-writer" "$STAGE1_PROMPT"

    AGENT_WR_DUR=$LAST_AGENT_DURATION
    AGENT_WR_RES="passed"

    # Gate 1: el HCL debe ser valido
    log "Gate: verificando terraform validate..."
    (cd "$INFRA_ENV_DIR_ABS" && terraform init -backend=false -input=false >>"$LOG_FILE_ABS" 2>&1) \
        || abort "Stage 1 fallido: terraform init fallo"
    (cd "$INFRA_ENV_DIR_ABS" && terraform validate >>"$LOG_FILE_ABS" 2>&1) \
        || abort "Stage 1 fallido: terraform validate fallo. Revisa el log."
    success "Gate 1: HCL valido"

    # Auto-commit si hay cambios
    if [ -n "$(git status --porcelain -- infra/)" ]; then
        log "Commiteando cambios de HCL..."
        git add infra/
        git commit -m "infra($ENVIRONMENT): escritura HCL issue #${ISSUE_NUM}"
    fi

    update_status "1-infra-writer" "passed"
fi

# --- STAGE 2: infra-reviewer (plan y revision) ---
if [ "$FROM_STAGE" -le 2 ]; then
    header "Stage 2: infra-reviewer (revision y plan)"

    DIFF_CONTEXT=$(git diff main...HEAD -- infra/ 2>/dev/null | head -200 || echo "(sin diff disponible)")

    STAGE2_PROMPT="Estas en el directorio raiz del proyecto ControlAsistencias.

Contexto del issue:

$ISSUE_CONTEXT

Ambiente target: $ENVIRONMENT
Directorio del ambiente: $INFRA_ENV_DIR_ABS

Diff de archivos .tf modificados en esta rama:
$DIFF_CONTEXT

Tu tarea: revisa el HCL producido por infra-writer, corrige problemas de seguridad o calidad, y ejecuta 'terraform plan -out=tfplan' en '$INFRA_ENV_DIR_ABS'. Sigue todas las instrucciones de tu rol de infra-reviewer."

    run_agent "2" "infra-reviewer" "$STAGE2_PROMPT"

    AGENT_RV_DUR=$LAST_AGENT_DURATION
    AGENT_RV_RES="passed"

    # Gate 2: el tfplan debe existir
    [ -f "$INFRA_ENV_DIR_ABS/tfplan" ] \
        || abort "Stage 2 fallido: el infra-reviewer no genero el archivo tfplan en $INFRA_ENV_DIR_ABS"
    success "Gate 2: tfplan generado"

    # Commit de correcciones del reviewer si las hubo
    if [ -n "$(git status --porcelain -- infra/)" ]; then
        log "Commiteando correcciones del reviewer..."
        git add infra/
        git commit -m "infra($ENVIRONMENT): correcciones de revision issue #${ISSUE_NUM}"
    fi

    update_status "2-infra-reviewer" "passed"
fi

# --- STAGE 3: infra-applier (aplicar) ---
if [ "$SKIP_APPLY" = true ]; then
    warn "Flag --skip-apply activo: omitiendo Stage 3 (apply)"
    update_status "skip-apply" "completed"
else
    if [ "$FROM_STAGE" -le 3 ]; then
        header "Stage 3: infra-applier (aplicar)"

        if [ "$AUTO_APPLY" = true ]; then
            export IAC_AUTO_APPLY=true
            log "Modo auto-apply activo (ambiente: $ENVIRONMENT)"
        fi

        STAGE3_PROMPT="Estas en el directorio raiz del proyecto ControlAsistencias.

El infra-reviewer ya genero el plan de Terraform en: $INFRA_ENV_DIR_ABS/tfplan
Ambiente: $ENVIRONMENT
Auto-apply: $AUTO_APPLY

Tu tarea: aplica el plan Terraform pre-generado siguiendo todas las instrucciones de tu rol de infra-applier."

        run_agent "3" "infra-applier" "$STAGE3_PROMPT"

        AGENT_AP_DUR=$LAST_AGENT_DURATION
        AGENT_AP_RES="passed"
        update_status "3-infra-applier" "passed"
    fi
fi

# --- Crear PR ---
header "Creando Pull Request"

git push origin "$BRANCH_NAME" >>"$LOG_FILE_ABS" 2>&1 \
    || abort "No se pudo hacer push de la rama $BRANCH_NAME"

APPLY_STATUS="pendiente de aplicar"
[ "$SKIP_APPLY" = false ] && [ "$AGENT_AP_RES" = "passed" ] && APPLY_STATUS="aplicado en $ENVIRONMENT"

PR_URL=$(gh pr create \
    --title "infra($ENVIRONMENT): #$ISSUE_NUM $ISSUE_TITLE" \
    --body "$(cat <<PREOF
## Infraestructura

Implementa los cambios de infraestructura del issue #$ISSUE_NUM.

- **Ambiente**: $ENVIRONMENT
- **Estado**: $APPLY_STATUS
- **Pipeline**: iac-pipeline.sh

## Cambios Terraform

$(git diff main...HEAD --stat -- infra/ 2>/dev/null || echo "(ver diff del PR)")

Closes #$ISSUE_NUM
PREOF
)" \
    --base main \
    2>>"$LOG_FILE_ABS") || warn "No se pudo crear el PR automaticamente"

[ -n "${PR_URL:-}" ] && success "PR creado: $PR_URL"

# --- Historial ---
echo "{\"issue\":\"$ISSUE_NUM\",\"title\":\"$(echo "$ISSUE_TITLE" | sed 's/"/\\"/g')\",\"environment\":\"$ENVIRONMENT\",\"started\":\"$TIMESTAMP\",\"completed\":\"$(date +%Y-%m-%dT%H:%M:%S)\",\"agents\":{\"infra-writer\":{\"duration\":${AGENT_WR_DUR:-null},\"result\":\"$AGENT_WR_RES\"},\"infra-reviewer\":{\"duration\":${AGENT_RV_DUR:-null},\"result\":\"$AGENT_RV_RES\"},\"infra-applier\":{\"duration\":${AGENT_AP_DUR:-null},\"result\":\"$AGENT_AP_RES\"}},\"pr\":\"${PR_URL:-}\"}" \
    >> "$PIPELINE_DIR_ABS/infra-history.jsonl"

update_status "completed" "completed"

echo -e "\n${GREEN}${BOLD}Pipeline IaC completado${NC}"
echo -e "  Issue:     #$ISSUE_NUM — $ISSUE_TITLE"
echo -e "  Ambiente:  $ENVIRONMENT"
[ -n "${PR_URL:-}" ] && echo -e "  PR:        $PR_URL"
echo -e "  Log:       $LOG_FILE_ABS"
