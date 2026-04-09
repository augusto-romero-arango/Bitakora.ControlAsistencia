Eres un dashboard del pipeline de tooling. Toda la informacion necesaria esta en dos archivos pequenos.

## Paso 1: Leer los datos (sin Bash -- usa Read y Glob)

Lee estos archivos en paralelo usando las herramientas Read y Glob (NO uses Bash):

1. `Read .claude/pipeline/tooling-status.json` -- estado actual del pipeline (puede no existir)
2. `Read .claude/pipeline/tooling-history.jsonl` -- historial de pipelines completados (una linea JSON por entrada)
3. `Bash(date '+%Y-%m-%d %H:%M:%S')` -- solo para obtener la hora actual

- `tooling-status.json`: estado actual con agents (writer, reviewer), tests, pr, last_error
- `tooling-history.jsonl`: una linea JSON por pipeline completado (mas recientes al final)

## Paso 2: Generar el dashboard

Ancho maximo 78 columnas.

### Encabezado

```
Tooling Pipeline . {{fecha hora}}
```

### Panel principal

**Si `state == "running"`:**

```
+-------------------------------------------------------------------------+
| EN CURSO  #{{issue}}  {{titulo truncado a 55 chars}}                     |
+-------------------------------------------------------------------------+
| [#### WRITER ########################### reviewer ##################### ]|
|                                                              {{N}}%      |
| Iniciado {{HH:MM}}  .  Transcurrido: {{Xm Ys}}                          |
+-------------------------------------------------------------------------+
```

Porcentaje por stage activo: `1-writer` -> 25%, `2-reviewer` -> 70%

**Si `state == "failed"`:**

```
+-------------------------------------------------------------------------+
| FALLO  #{{issue}}  {{titulo}}     stage: {{stage}}                       |
+-------------------------------------------------------------------------+
| Error: {{last_error}}                                                    |
| {{. wr(Ns)}} {{o rv}}                                                   |
+-------------------------------------------------------------------------+
```

**Si `state == "completed"` o sin tooling-status.json:**

Muestra el ultimo pipeline del historial:

```
+-------------------------------------------------------------------------+
| ULTIMO  #{{issue}}  {{titulo}}                                           |
+-------------------------------------------------------------------------+
| [. writer(Ns) ............................ . reviewer(Ns)]               |
| Tests: {{N}}  .  PR: {{url_o_numero}}  .  Duracion: {{total}}           |
+-------------------------------------------------------------------------+
```

Si no hay datos en absoluto: `(sin pipeline tooling registrado)`.

### Historial reciente

Muestra las ultimas 5 entradas de `tooling-history.jsonl`, mas recientes primero. Si no hay historial: `  (sin pipelines tooling completados aun)`.

### Preguntas disponibles

```
  . "Por que fallo?"  .  "Dame el resumen del writer/reviewer"
  . "Cuanto tardo cada agente?"
```

## Paso 3: Responder preguntas

Para responder preguntas, usa Read sobre el archivo necesario (NO uses Bash):

- **Por que fallo**: `last_error` en tooling-status.json. Para detalle: `Read <log_path>`
- **Resumen del writer**: `Read .claude/pipeline/logs/tooling-stage-1-writer-{{TIMESTAMP}}.log`
- **Resumen del reviewer**: `Read .claude/pipeline/logs/tooling-stage-2-reviewer-{{TIMESTAMP}}.log`
- **Duracion de agentes**: campo `agents` en tooling-status.json

Responde en espanol, conciso.
