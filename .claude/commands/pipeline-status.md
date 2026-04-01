Eres un dashboard del pipeline TDD. Toda la información necesaria está en dos archivos pequeños.

## Paso 1: Leer los datos (un solo comando Bash)

```bash
echo "===STATUS===" && cat .claude/pipeline/status.json 2>/dev/null || echo "null" && \
echo "===PARALLEL===" && for f in .claude/pipeline/status-*.json; do [ -f "$f" ] && echo "---FILE:$f---" && cat "$f"; done 2>/dev/null || true && \
echo "===HISTORY===" && tail -10 .claude/pipeline/history.jsonl 2>/dev/null || true && \
echo "===TIME===" && date '+%Y-%m-%d %H:%M:%S'
```

- `status.json`: estado actual de un pipeline individual con agents, tests, pr, last_error ya incluidos
- `status-*.json`: archivos de estado de pipelines paralelos (uno por issue cuando se usa parallel-pipeline.sh)
- `history.jsonl`: una línea JSON por pipeline completado (más recientes al final)
- `date`: para calcular tiempo transcurrido y determinar "última hora"

## Paso 2: Generar el dashboard

Ancho máximo 78 columnas.

### Encabezado

```
TDD Pipeline · {{fecha hora}}
```

### Panel principal

**Si `state == "running"`:**

La barra de progreso integra las 3 etapas en 60 chars. El agente activo aparece nombrado en MAYÚSCULAS, los completados con `●`, los pendientes con `·`:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ◉ EN CURSO  #{{issue}}  {{título truncado a 55 chars}}                 │
├─────────────────────────────────────────────────────────────────────────┤
│ [■■■■ TEST-WRITER ·················· implementer ·········· reviewer ] │
│                                                              {{N}}%     │
│ Iniciado {{HH:MM}}  ·  Transcurrido: {{Xm Ys}}                        │
└─────────────────────────────────────────────────────────────────────────┘
```

Porcentaje por stage activo: `1-test-writer` → 15%, `2-implementer` → 50%, `3-reviewer` → 80%

Agentes completados (`result == "passed"`): muestra `● nombre` con su duración: `● tw(203s)`

**Si `state == "failed"`:**

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ✗ FALLÓ  #{{issue}}  {{título}}     stage: {{stage}}                   │
├─────────────────────────────────────────────────────────────────────────┤
│ Error: {{last_error}}                                                   │
│ {{● tw(Ns)}} {{● im(Ns) | ○ im}} {{○ rv}}                              │
└─────────────────────────────────────────────────────────────────────────┘
```

**Si existen archivos `status-*.json` (pipeline paralelo activo):**

Mostrar un panel multi-issue con todos los archivos `status-*.json` encontrados:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ◉ PARALELO  N issues en proceso                                         │
├─────────────────────────────────────────────────────────────────────────┤
│  #42  TEST-WRITER    0m 45s                                             │
│  #43  IMPLEMENTER    3m 20s   tw:180s                                   │
│  #44  REVIEWER       8m 40s   tw:120s im:400s                           │
└─────────────────────────────────────────────────────────────────────────┘
```

Cada línea muestra el issue, el stage activo en MAYÚSCULAS, el tiempo transcurrido, y los agentes completados con su duración. Los issues completados muestran el PR en lugar del stage. Los fallidos muestran el error.

**Si `state == "completed"` o sin status.json:**

Muestra el último pipeline del historial:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ ✓ ÚLTIMO  #{{issue}}  {{título}}                                       │
├─────────────────────────────────────────────────────────────────────────┤
│ [● test-writer(Ns) ············ ● implementer(Ns) ······ ● reviewer(Ns)]│
│ Tests: {{N}}  ·  PR: {{url_o_numero}}  ·  Duración: {{total}}          │
└─────────────────────────────────────────────────────────────────────────┘
```

Si no hay datos en absoluto: `(sin pipeline registrado)`.

### Historial reciente

```
──────────────────────────────────────────────────────────────────────────
  HISTORIAL
──────────────────────────────────────────────────────────────────────────
  #{{issue}}  ✓  {{dur_total}}  │  {{N}} tests  │  PR: #{{N}}
              tw:{{N}}s → im:{{N}}s → rv:{{N}}s
```

Muestra las últimas 5 entradas de `history.jsonl`, más recientes primero. Omite el pipeline activo si ya aparece en el panel principal. Si no hay historial: `  (sin pipelines completados aún)`.

La duración total = suma de duraciones de los 3 agentes.

### Preguntas disponibles

```
──────────────────────────────────────────────────────────────────────────
  ◆ "¿Por qué falló?"  ·  "¿Qué tests se escribieron en el #{{issue}}?"
  ◆ "Dame el resumen del reviewer"  ·  "¿Cuánto tardó cada agente?"
──────────────────────────────────────────────────────────────────────────
```

## Paso 3: Responder preguntas

Para responder preguntas, ejecuta un segundo Bash leyendo SOLO el archivo necesario:

- **¿Por qué falló?**: `last_error` ya está en status.json. Para más detalle: `tail -30 <log_path>` usando el campo `log` del status.json
- **Tests escritos**: `cat .claude/pipeline/logs/stage-1-test-writer-{{TIMESTAMP}}.log`
- **Resumen del reviewer**: `cat .claude/pipeline/logs/stage-3-reviewer-{{TIMESTAMP}}.log`
- **Duración de agentes**: ya en `status.json` → campo `agents`
- **PR**: campo `pr` en status.json o history.jsonl

El timestamp del log coincide con el campo `started` del pipeline. Si el usuario no especifica issue, usa el más reciente del historial.

Responde en español, conciso, con listas `◆` o tablas cuando sea apropiado.
