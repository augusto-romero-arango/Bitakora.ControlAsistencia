#!/usr/bin/env bash
# _pipeline-common.sh --- Funciones compartidas entre scripts de pipeline
#
# Uso: source "$(dirname "${BASH_SOURCE[0]}")/_pipeline-common.sh"
#
# No invocar directamente (prefijo _ = sourceable).

# resolve_pipeline <issue_num> [override]
#
# Retorna la ruta del script de pipeline a usar para un issue dado.
# - Sin override: consulta labels del issue via gh y enruta automaticamente
# - Con override "tdd" o "tooling": retorna el pipeline forzado sin consultar labels
# - Issues tipo:infra retornan "SKIP:infra"
# - Issues sin label tipo:* retornan "SKIP:no-tipo"
resolve_pipeline() {
    local issue="$1"
    local override="${2:-}"

    if [ -n "$override" ]; then
        case "$override" in
            tdd)     echo "./scripts/tdd-pipeline.sh" ;;
            tooling) echo "./scripts/tooling-pipeline.sh" ;;
            *)       echo "ERROR: override desconocido '$override'" >&2; return 1 ;;
        esac
        return
    fi

    local labels
    labels=$(gh issue view "$issue" --json labels -q '.labels[].name' 2>/dev/null)

    _resolve_from_labels "$labels"
}

# _resolve_from_labels <labels_text>
# Funcion interna: determina el pipeline a partir de texto de labels (una por linea).
_resolve_from_labels() {
    local labels="$1"
    if echo "$labels" | grep -qE '^tipo:(feature|refactor|bug)$'; then
        echo "./scripts/tdd-pipeline.sh"
    elif echo "$labels" | grep -q '^tipo:tooling$'; then
        echo "./scripts/tooling-pipeline.sh"
    elif echo "$labels" | grep -q '^tipo:infra$'; then
        echo "SKIP:infra"
    else
        echo "SKIP:no-tipo"
    fi
}

# resolve_pipeline_with_state <issue_num> [override]
#
# Retorna "STATE|PIPELINE" en una sola linea (ej: "OPEN|./scripts/tdd-pipeline.sh").
# Combina la consulta de estado y labels en una sola llamada a gh, reduciendo API calls.
resolve_pipeline_with_state() {
    local issue="$1"
    local override="${2:-}"

    local state_and_labels
    state_and_labels=$(gh issue view "$issue" --json state,labels \
        -q '"\(.state)|\(.labels | map(.name) | join("\n"))"' 2>/dev/null) || {
        echo "UNKNOWN|SKIP:no-tipo"
        return
    }

    local state="${state_and_labels%%|*}"
    local labels="${state_and_labels#*|}"

    if [ -n "$override" ]; then
        case "$override" in
            tdd)     echo "$state|./scripts/tdd-pipeline.sh" ;;
            tooling) echo "$state|./scripts/tooling-pipeline.sh" ;;
            *)       echo "ERROR: override desconocido '$override'" >&2; return 1 ;;
        esac
        return
    fi

    local pipeline
    pipeline=$(_resolve_from_labels "$labels")
    echo "$state|$pipeline"
}
