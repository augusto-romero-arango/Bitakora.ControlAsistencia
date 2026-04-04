Lanza el pipeline TDD secuencial (batch) para multiples issues dentro de una sesion tmux. Cada issue se implementa, se crea PR y se mergea a main antes de continuar con el siguiente. Comunicate en **espanol**.

## Entrada

Los numeros de issues estan en: $ARGUMENTS

Si `$ARGUMENTS` esta vacio, responde: `Uso: /batch <issue1> <issue2> <issue3> ...`

## Proceso

### 1. Validar los issues

Para cada numero en los argumentos:

```bash
gh issue view <num> --json number,title,state -q '"#\(.number): \(.title) [\(.state)]"'
```

Si algun issue no existe o esta cerrado, informalo y excluyelo de la lista. Si no queda ningun issue valido, detente.

### 2. Mostrar resumen y lanzar

Muestra la lista de issues que se procesaran en orden:

```
Batch secuencial — 3 issues:
  1. #42: Implementar calculo de horas extras nocturnas
  2. #43: Agregar validacion de jornada maxima
  3. #44: Calcular recargos dominicales
```

Luego lanza:

```bash
./scripts/tmux-pipeline.sh --batch <issue1> <issue2> <issue3>
```

### 3. Instrucciones de conexion

Responde con:

```
Batch lanzado en tmux. Para monitorear:
  tmux -CC attach -t batch-<timestamp>

Los issues se procesaran en orden: implementa → PR → merge → siguiente.
Usa /pipeline-status para ver el progreso sin salir de aqui.
```

## Reglas

- **No esperes a que termine.** Devuelve el control inmediatamente.
- **No implementes nada tu mismo.** Solo lanza el script.
- Si el usuario pasa `--stop-on-error`, pasalo al script batch-pipeline.sh (pero tmux-pipeline.sh no lo soporta directamente, asi que informale que use `./scripts/batch-pipeline.sh` directo para esa opcion).
