#!/usr/bin/env bash
# tmux-pipeline.sh — Wrapper opcional para ejecutar pipelines dentro de sesiones tmux
#
# Uso:
#   ./scripts/tmux-pipeline.sh 42                        # issue unico
#   ./scripts/tmux-pipeline.sh --batch 42 43 44          # secuencial (batch)
#   ./scripts/tmux-pipeline.sh --parallel 42 43 44       # paralelo
#   ./scripts/tmux-pipeline.sh --parallel 42 43 --max-parallel 2
#   ./scripts/tmux-pipeline.sh --attach                  # reconectar sesion existente
#   ./scripts/tmux-pipeline.sh --attach tdd-42           # reconectar sesion especifica
#
# Recomendado: ejecutar desde iTerm2 con tmux -CC para UI nativa.
# Los scripts subyacentes (tdd-pipeline.sh, batch-pipeline.sh, parallel-pipeline.sh)
# no se modifican y siguen funcionando independientemente.

set -euo pipefail

# --- Colores ---
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
EVENTS_LOG="$PROJECT_ROOT/.claude/pipeline/events.log"

log()     { echo -e "${BLUE}[$(date +%H:%M:%S)]${NC} $1"; }
success() { echo -e "${GREEN}${BOLD}✓${NC} $1"; }
warn()    { echo -e "${YELLOW}⚠${NC} $1"; }
abort()   { echo -e "\n${RED}${BOLD}✗ $1${NC}" >&2; exit 1; }

# --- Verificaciones previas ---
check_tmux() {
    if ! command -v tmux &>/dev/null; then
        abort "tmux no esta instalado. Instala con: brew install tmux"
    fi
}

# Detectar si estamos dentro de una sesion tmux activa
inside_tmux() {
    [ -n "${TMUX:-}" ]
}

# Asegurar que events.log existe para que tail no falle
ensure_events_log() {
    mkdir -p "$(dirname "$EVENTS_LOG")"
    touch "$EVENTS_LOG"
}

# Nombre de sesion seguro para tmux (sin espacios ni caracteres especiales)
safe_session_name() {
    echo "$1" | tr ' /:' '-' | tr -cd 'a-zA-Z0-9-'
}

# Verificar si una sesion tmux existe
session_exists() {
    tmux has-session -t "$1" 2>/dev/null
}

# Imprimir instrucciones de conexion
print_connect_hint() {
    local session="$1"
    echo ""
    echo -e "${CYAN}${BOLD}Sesion tmux lista: $session${NC}"
    echo ""
    echo -e "  ${BOLD}En iTerm2 (recomendado):${NC}"
    echo -e "    tmux -CC attach -t $session"
    echo ""
    echo -e "  ${BOLD}En terminal estandar:${NC}"
    echo -e "    tmux attach -t $session"
    echo ""
    echo -e "  ${BOLD}Ver todas las sesiones:${NC}"
    echo -e "    tmux ls"
    echo ""
}

# --- Modo ATTACH ---
cmd_attach() {
    local target="${1:-}"
    check_tmux

    if [ -n "$target" ]; then
        if ! session_exists "$target"; then
            # Mostrar sesiones disponibles
            echo -e "${YELLOW}Sesion '$target' no existe. Sesiones disponibles:${NC}"
            tmux ls 2>/dev/null || echo "  (ninguna)"
            exit 1
        fi
        exec tmux attach -t "$target"
    else
        # Adjuntar a la primera sesion disponible
        if ! tmux ls &>/dev/null; then
            abort "No hay sesiones tmux activas."
        fi
        exec tmux attach
    fi
}

# --- Modo SINGLE (un issue) ---
cmd_single() {
    local issue="$1"
    local extra_args="${2:-}"
    local session
    session=$(safe_session_name "tdd-$issue")

    check_tmux
    ensure_events_log

    if session_exists "$session"; then
        warn "Ya existe una sesion '$session'."
        print_connect_hint "$session"
        exit 0
    fi

    log "Creando sesion tmux '$session' para issue #$issue..."

    # Crear sesion con ventana unica y panes lado a lado
    tmux new-session -d -s "$session" -n "main" -c "$PROJECT_ROOT"
    tmux set-option -t "$session" remain-on-exit on
    tmux send-keys -t "$session:main" "tail -f '$EVENTS_LOG'" Enter

    # Pane derecho: pipeline
    tmux split-window -h -t "$session:main" -c "$PROJECT_ROOT"
    tmux send-keys -t "$session:main.1" "./scripts/tdd-pipeline.sh $issue $extra_args" Enter

    tmux select-layout -t "$session:main" even-horizontal

    success "Pipeline iniciado para issue #$issue"
    print_connect_hint "$session"
}

# --- Modo BATCH (secuencial) ---
cmd_batch() {
    local issues=("$@")
    local session
    session=$(safe_session_name "batch-$(date +%H%M%S)")
    local issues_str="${issues[*]}"

    check_tmux
    ensure_events_log

    log "Creando sesion tmux '$session' para batch: issues ${issues_str}..."

    # Crear sesion con ventana unica y panes lado a lado
    tmux new-session -d -s "$session" -n "main" -c "$PROJECT_ROOT"
    tmux set-option -t "$session" remain-on-exit on
    tmux send-keys -t "$session:main" "tail -f '$EVENTS_LOG'" Enter

    # Pane derecho: batch pipeline
    tmux split-window -h -t "$session:main" -c "$PROJECT_ROOT"
    tmux send-keys -t "$session:main.1" "./scripts/batch-pipeline.sh $issues_str" Enter

    tmux select-layout -t "$session:main" even-horizontal

    success "Batch pipeline iniciado: issues $issues_str"
    print_connect_hint "$session"
}

# --- Modo PARALELO (un tab por issue) ---
cmd_parallel() {
    local max_parallel=""
    local issues=()

    # Parsear --max-parallel de los args restantes
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --max-parallel)
                max_parallel="$2"
                shift 2
                ;;
            --max-parallel=*)
                max_parallel="${1#*=}"
                shift
                ;;
            *)
                issues+=("$1")
                shift
                ;;
        esac
    done

    if [ ${#issues[@]} -eq 0 ]; then
        abort "Debes especificar al menos un issue. Uso: --parallel 42 43 44"
    fi

    local session
    session=$(safe_session_name "parallel-$(date +%H%M%S)")
    local issues_str="${issues[*]}"
    local max_flag=""
    [ -n "$max_parallel" ] && max_flag="--max-parallel $max_parallel"

    check_tmux
    ensure_events_log

    log "Creando sesion tmux '$session' para issues paralelos: $issues_str..."

    # Crear sesion con ventana unica y panes lado a lado
    tmux new-session -d -s "$session" -n "main" -c "$PROJECT_ROOT"
    tmux set-option -t "$session" remain-on-exit on
    tmux send-keys -t "$session:main" "tail -f '$EVENTS_LOG'" Enter

    # Un pane por issue
    for issue in "${issues[@]}"; do
        tmux split-window -h -t "$session:main" -c "$PROJECT_ROOT"
        tmux send-keys -t "$session:main" "./scripts/tdd-pipeline.sh $issue" Enter
    done

    tmux select-layout -t "$session:main" even-horizontal

    success "Pipeline paralelo iniciado: issues $issues_str"
    print_connect_hint "$session"

    # Nota: el flag --max-parallel se ignora aqui porque cada issue tiene su propio tab
    # Si se necesita limitar concurrencia, usar parallel-pipeline.sh directamente
    if [ -n "$max_parallel" ]; then
        warn "--max-parallel no aplica en modo tmux (cada issue tiene su propio tab)."
        warn "Para limitar concurrencia usa: ./scripts/parallel-pipeline.sh $max_flag $issues_str"
    fi
}

# --- Modo TOOLING (un issue, pipeline sin TDD) ---
cmd_tooling() {
    local issue="$1"
    local session
    session=$(safe_session_name "tooling-$issue")

    check_tmux
    ensure_events_log

    if session_exists "$session"; then
        warn "Ya existe una sesion '$session'."
        print_connect_hint "$session"
        exit 0
    fi

    log "Creando sesion tmux '$session' para tooling issue #$issue..."

    tmux new-session -d -s "$session" -n "main" -c "$PROJECT_ROOT"
    tmux set-option -t "$session" remain-on-exit on
    tmux send-keys -t "$session:main" "tail -f '$EVENTS_LOG'" Enter

    tmux split-window -h -t "$session:main" -c "$PROJECT_ROOT"
    tmux send-keys -t "$session:main.1" "./scripts/tooling-pipeline.sh $issue" Enter

    tmux select-layout -t "$session:main" even-horizontal

    success "Pipeline tooling iniciado para issue #$issue"
    print_connect_hint "$session"
}

# --- Modo SCAFFOLD (un dominio) ---
cmd_scaffold() {
    local issue=""
    local domain=""

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --domain)
                domain="$2"
                shift 2
                ;;
            --domain=*)
                domain="${1#*=}"
                shift
                ;;
            [0-9]*)
                issue="$1"
                shift
                ;;
            *)
                abort "Argumento desconocido para --scaffold: $1"
                ;;
        esac
    done

    [ -n "$domain" ] || abort "Falta --domain para --scaffold. Uso: --scaffold [issue] --domain nombre"

    # Normalizar dominio a kebab-case (acepta PascalCase, camelCase, snake_case)
    domain=$(echo "$domain" \
        | sed 's/_/-/g' \
        | sed 's/\([a-z0-9]\)\([A-Z]\)/\1-\2/g' \
        | tr '[:upper:]' '[:lower:]')

    local session
    session=$(safe_session_name "scaffold-$domain")

    check_tmux
    ensure_events_log

    if session_exists "$session"; then
        warn "Ya existe una sesion '$session'."
        print_connect_hint "$session"
        exit 0
    fi

    local pipeline_args=""
    [ -n "$issue" ] && pipeline_args="$issue "
    pipeline_args="${pipeline_args}--domain $domain"

    log "Creando sesion tmux '$session' para scaffold del dominio '$domain'..."

    tmux new-session -d -s "$session" -n "main" -c "$PROJECT_ROOT"
    tmux set-option -t "$session" remain-on-exit on
    tmux send-keys -t "$session:main" "tail -f '$EVENTS_LOG'" Enter

    tmux split-window -h -t "$session:main" -c "$PROJECT_ROOT"
    tmux send-keys -t "$session:main.1" "./scripts/scaffold-pipeline.sh $pipeline_args" Enter

    tmux select-layout -t "$session:main" even-horizontal

    success "Pipeline de scaffold iniciado para dominio '$domain'"
    print_connect_hint "$session"
}

# --- Mostrar ayuda ---
cmd_help() {
    cat <<EOF

${CYAN}${BOLD}tmux-pipeline.sh${NC} — Wrapper para pipelines en sesiones tmux

${BOLD}Uso:${NC}
  ./scripts/tmux-pipeline.sh 42                                   Issue unico (TDD)
  ./scripts/tmux-pipeline.sh --tooling 42                         Issue de tooling (sin TDD)
  ./scripts/tmux-pipeline.sh --scaffold 42 --domain nombre        Scaffold de dominio
  ./scripts/tmux-pipeline.sh --scaffold --domain nombre           Scaffold sin issue
  ./scripts/tmux-pipeline.sh --batch 42 43 44                     Secuencial (uno a la vez)
  ./scripts/tmux-pipeline.sh --parallel 42 43 44                  Paralelo (un tab por issue)
  ./scripts/tmux-pipeline.sh --attach                             Reconectar sesion tmux activa
  ./scripts/tmux-pipeline.sh --attach tdd-42                      Reconectar sesion especifica

${BOLD}En iTerm2 (recomendado):${NC}
  1. Corre el comando anterior desde tu terminal normal
  2. El script crea la sesion en background y te dice como conectarte
  3. Ejecuta: tmux -CC attach -t <nombre-sesion>
  4. iTerm2 muestra los panes lado a lado: 'dashboard' + 'pipeline' (o uno por issue)

${BOLD}Ver sesiones activas:${NC}
  tmux ls

${BOLD}Documentacion completa:${NC}
  docs/tmux-cheatsheet.md

EOF
}

# --- Entrypoint ---
main() {
    if [ $# -eq 0 ]; then
        cmd_help
        exit 0
    fi

    # Pre-parsear --scaffold-domain antes del dispatch de modo,
    # para que no interfiera con la deteccion de multiples issues
    local scaffold_extra=""
    local filtered_args=()
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --scaffold-domain)
                [ $# -lt 2 ] && abort "Falta el nombre del dominio para --scaffold-domain"
                scaffold_extra="--scaffold-domain $2"
                shift 2
                ;;
            *)
                filtered_args+=("$1")
                shift
                ;;
        esac
    done
    set -- "${filtered_args[@]}"

    case "$1" in
        --help|-h)
            cmd_help
            ;;
        --attach)
            shift
            cmd_attach "${1:-}"
            ;;
        --tooling)
            shift
            if [ $# -eq 0 ]; then
                abort "Debes especificar un issue. Uso: --tooling 42"
            fi
            cmd_tooling "$1"
            ;;
        --scaffold)
            shift
            cmd_scaffold "$@"
            ;;
        --batch)
            shift
            if [ $# -eq 0 ]; then
                abort "Debes especificar al menos un issue. Uso: --batch 42 43 44"
            fi
            cmd_batch "$@"
            ;;
        --parallel)
            shift
            if [ $# -eq 0 ]; then
                abort "Debes especificar al menos un issue. Uso: --parallel 42 43 44"
            fi
            cmd_parallel "$@"
            ;;
        [0-9]*)
            # Modo single: argumento directo es un issue
            if [ $# -gt 1 ]; then
                warn "Multiples issues sin modo especificado. Usando --parallel."
                cmd_parallel "$@"
            else
                cmd_single "$1" "$scaffold_extra"
            fi
            ;;
        *)
            echo -e "${RED}Argumento desconocido: $1${NC}"
            cmd_help
            exit 1
            ;;
    esac
}

main "$@"
