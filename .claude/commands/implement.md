Lanza el pipeline TDD para un issue de GitHub dentro de una sesion tmux. Comunicate en **espanol**.

## Entrada

El numero de issue esta en: $ARGUMENTS

Si `$ARGUMENTS` esta vacio, responde: `Uso: /implement <numero-de-issue>`

## Proceso

### 1. Validar el issue

```bash
gh issue view $ARGUMENTS --json number,title,state,labels -q '"#\(.number): \(.title) [\(.state)] labels: \([.labels[].name] | join(", "))"'
```

Si el issue no existe o esta cerrado (`CLOSED`), informa y detente.

### 2. Mostrar info y lanzar

Muestra una linea con el issue:

```
#42: Implementar calculo de horas extras nocturnas
Dominio: Liquidacion | Tipo: feature | Estado: listo
```

Luego lanza el pipeline en tmux:

```bash
./scripts/tmux-pipeline.sh $ARGUMENTS
```

### 3. Instrucciones de conexion

Responde con:

```
Pipeline lanzado en tmux. Para monitorear:
  tmux -CC attach -t tdd-<numero>

Usa /pipeline-status para ver el progreso sin salir de aqui.
```

## Reglas

- **No esperes a que termine.** El script corre en background dentro de tmux. Devuelve el control inmediatamente.
- **No implementes nada tu mismo.** Solo lanza el script.
- Si tmux no esta instalado, el script lo detecta y muestra el error. No intentes instalarlo.
