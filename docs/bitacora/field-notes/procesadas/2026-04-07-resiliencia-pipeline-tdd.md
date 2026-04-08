---
fecha: 2026-04-07
hora: 15:00
sesion: incidente-y-mejora
tema: Resiliencia del pipeline TDD - deteccion de bloqueo y continuacion
---

## Incidente

El pipeline TDD para el issue #10 (Solicitar programacion de turno del catalogo) fallo por timeout del implementer despues de ~95 minutos. El timeout configurado era 1800s (30 min).

### Causa raiz: dos bugs

1. **Timeout no mataba al proceso correcto**: el watchdog hacia `kill -9 $PID` sobre el subshell `(cd && claude ...)`, pero el proceso `claude` hijo quedaba como huerfano y seguia corriendo. Fix: `kill -9 -$PID` (process group completo).

2. **Pipeline "todo o nada"**: si el implementer no lograba poner todos los tests en verde, el pipeline abortaba sin crear PR. Todo el progreso parcial se perdia.

### Recuperacion

El implementer SI habia completado su trabajo — el commit existia en el worktree con 109/109 tests pasando. Se recupero manualmente: rebase sobre main, ejecucion del reviewer, creacion del PR (#14).

## Decision: deteccion de bloqueo y continuacion

### Problema

Cuando el implementer entra en un loop intentando resolver un test que no puede hacer pasar, consume todo el timeout sin producir resultado util. El pipeline aborta y el humano no tiene visibilidad de que se intento.

### Diseno implementado

```
Implementer (5 intentos) -> yield con reporte -> Reviewer (5 intentos) -> PR con tests rojos
```

**Definicion de "intento"**: un intento cuenta solo cuando el agente deliberadamente enfoca su trabajo en resolver un test especifico con un enfoque distinto. No cuentan fallos incidentales mientras trabaja en otros tests ni test runs de verificacion general.

**Orden de trabajo**: el implementer debe completar todo lo que puede antes de declarar bloqueo. Solo cuando el unico trabajo pendiente es un test que no logra resolver, empieza a contar intentos.

**Flujo**:
1. Implementer detecta que gira en circulos (5 enfoques distintos, mismo test falla)
2. Hace commit de progreso parcial, escribe reporte en `.claude/pipeline/blockage-report.md`
3. Termina normalmente (exit 0, no timeout)
4. Pipeline detecta reporte, marca `HAS_BLOCKAGE=true`, continua al reviewer
5. Reviewer lee reporte, intenta resolver con 5 intentos adicionales (solo cambia implementacion, nunca tests)
6. Si persiste, actualiza reporte y termina normalmente
7. Pipeline crea PR con label `bloqueado` y el reporte como comentario

### Alternativa descartada: pedir intervencion humana mid-pipeline

Se considero que el implementer pidiera al humano que interviniera y luego retomara. Descartado por complejidad: requeria mecanismo de pausa/resume, y el beneficio de tener al reviewer como segundo intento es mayor con menor friccion.

## Archivos modificados

- `.claude/agents/implementer.md` — seccion 4b (deteccion de bloqueo) + regla 16
- `.claude/agents/reviewer.md` — seccion 2b (manejo de tests rojos heredados)
- `scripts/tdd-pipeline.sh` — gates tolerantes, prompt con contexto, PR con label

## Commits

- `6469f05` fix(pipeline): matar process group completo en timeout
- `c12693e` feat(pipeline): deteccion de bloqueo y continuacion con tests rojos
