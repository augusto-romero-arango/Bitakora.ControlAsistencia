Eres un dashboard del pipeline IaC. Toda la informacion necesaria esta en dos archivos pequeños.

## Paso 1: Leer los datos (un solo comando Bash)

```bash
echo "===STATUS===" && cat .claude/pipeline/infra-status.json 2>/dev/null || echo "null" && \
echo "===HISTORY===" && tail -10 .claude/pipeline/infra-history.jsonl 2>/dev/null || true && \
echo "===TIME===" && date '+%Y-%m-%d %H:%M:%S'
```

- `infra-status.json`: estado actual del pipeline IaC (issue, ambiente, agentes, error)
- `infra-history.jsonl`: una linea JSON por pipeline completado (mas recientes al final)
- `date`: para calcular tiempo transcurrido

## Paso 2: Generar el dashboard

Ancho maximo 78 columnas.

### Encabezado

```
IaC Pipeline · {{fecha hora}}
```

### Panel principal

**Si `state == "running"`:**

```
+-------------------------------------------------------------------------+
| EN CURSO  #{{issue}}  {{titulo truncado}}  env: {{ambiente}}           |
+-------------------------------------------------------------------------+
| [## INFRA-WRITER ............... infra-reviewer ......... infra-applier]|
|                                                              {{N}}%     |
| Iniciado {{HH:MM}}  .  Transcurrido: {{Xm Ys}}                        |
+-------------------------------------------------------------------------+
```

Porcentaje por stage activo: `1-infra-writer` → 20%, `2-infra-reviewer` → 55%, `3-infra-applier` → 85%

**Si `state == "failed"`:**

```
+-------------------------------------------------------------------------+
| FALLO  #{{issue}}  {{titulo}}     stage: {{stage}}   env: {{ambiente}} |
+-------------------------------------------------------------------------+
| Error: {{last_error}}                                                   |
+-------------------------------------------------------------------------+
```

**Si `state == "completed"` o sin infra-status.json:**

Muestra el ultimo pipeline del historial:

```
+-------------------------------------------------------------------------+
| ULTIMO  #{{issue}}  {{titulo}}  env: {{ambiente}}                      |
+-------------------------------------------------------------------------+
| [- writer(Ns) ........ - reviewer(Ns) .............. - applier(Ns)]    |
| Duracion: {{total}}                                                     |
+-------------------------------------------------------------------------+
```

Si no hay datos en absoluto: `(sin pipeline IaC registrado)`.

### Historial reciente

```
--------------------------------------------------------------------------
  HISTORIAL IaC
--------------------------------------------------------------------------
  #{{issue}}  v  {{dur_total}}  |  env: {{ambiente}}
              writer:{{N}}s -> reviewer:{{N}}s -> applier:{{N}}s
```

Muestra las ultimas 5 entradas de `infra-history.jsonl`, mas recientes primero.
Si no hay historial: `  (sin pipelines IaC completados aun)`.

### Preguntas disponibles

```
--------------------------------------------------------------------------
  * "¿Por que fallo?"  .  "¿Que recursos se crearon en el #{{issue}}?"
  * "Dame el plan del reviewer"  .  "¿Cuanto tardo cada agente?"
--------------------------------------------------------------------------
```

## Paso 3: Responder preguntas

Para responder preguntas, ejecuta un segundo Bash leyendo SOLO el archivo necesario:

- **¿Por que fallo?**: `last_error` ya esta en infra-status.json. Para mas detalle: `tail -30 <log_path>`
- **Recursos creados**: `cat .claude/pipeline/logs/iac-stage-2-infra-reviewer-{{TIMESTAMP}}.log | grep -E "# |will be"`
- **Plan del reviewer**: `cat .claude/pipeline/logs/iac-stage-2-infra-reviewer-{{TIMESTAMP}}.log`
- **Duracion de agentes**: ya en `infra-status.json` campo `agents`

El timestamp del log coincide con el campo `started` del pipeline.

Responde en espanol, conciso, con listas o tablas cuando sea apropiado.
