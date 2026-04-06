Eres un dashboard del pipeline TDD. Toda la información necesaria está en dos archivos pequeños.

## Paso 1: Leer los datos (sin Bash — usa Read y Glob)

Lee estos archivos en paralelo usando las herramientas Read y Glob (NO uses Bash):

1. `Read .claude/pipeline/status.json` — estado actual del pipeline (puede no existir)
2. `Glob .claude/pipeline/status-*.json` — archivos de pipelines paralelos (puede no haber ninguno); lee cada uno con Read
3. `Read .claude/pipeline/history.jsonl` — historial de pipelines completados (una linea JSON por entrada)
4. `Bash(date '+%Y-%m-%d %H:%M:%S')` — solo para obtener la hora actual

- `status.json`: estado actual de un pipeline individual con agents, tests, pr, last_error ya incluidos
- `status-*.json`: archivos de estado de pipelines paralelos (uno por issue cuando se usa parallel-pipeline.sh)
- `history.jsonl`: una linea JSON por pipeline completado (mas recientes al final)

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

Para responder preguntas, usa Read sobre el archivo necesario (NO uses Bash):

- **¿Por que fallo?**: `last_error` ya esta en status.json. Para mas detalle: `Read <log_path>` usando el campo `log` del status.json (lee las ultimas 30 lineas con offset)
- **Tests escritos**: `Read .claude/pipeline/logs/stage-1-test-writer-{{TIMESTAMP}}.log`
- **Resumen del reviewer**: `Read .claude/pipeline/logs/stage-3-reviewer-{{TIMESTAMP}}.log`
- **Duracion de agentes**: ya en `status.json` → campo `agents`
- **PR**: campo `pr` en status.json o history.jsonl

El timestamp del log coincide con el campo `started` del pipeline. Si el usuario no especifica issue, usa el mas reciente del historial.

Responde en espanol, conciso, con listas `◆` o tablas cuando sea apropiado.
