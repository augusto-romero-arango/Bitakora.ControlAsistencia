Lanza el pipeline de tooling para un issue de GitHub dentro de una sesion tmux. Comunicate en **espanol**.

## Entrada

El numero de issue esta en: $ARGUMENTS

Si `$ARGUMENTS` esta vacio, responde: `Uso: /tooling <numero-de-issue>`

## Proceso

### 1. Validar el issue

```bash
gh issue view $ARGUMENTS --json number,title,state,labels -q '"#\(.number): \(.title) [\(.state)] labels: \([.labels[].name] | join(", "))"'
```

Si el issue no existe o esta cerrado (`CLOSED`), informa y detente.

### 2. Validar que es una tarea de tooling

Extrae labels del issue:

```bash
gh issue view $ARGUMENTS --json labels -q '[.labels[].name] | join(",")'
```

Verifica que tenga el label `tipo:tooling`. Si no lo tiene, advierte al usuario:

```
Este issue no tiene el label tipo:tooling.
Si es logica de dominio, usa /implement en su lugar.
¿Continuar de todos modos? (s/n)
```

### 3. Mostrar info y lanzar

Muestra una linea con el issue:

```
#18: Implementar smoke tests para Service Bus triggers
Tipo: tooling | Estado: listo
```

Luego lanza el pipeline en tmux:

```bash
./scripts/tmux-pipeline.sh --tooling $ARGUMENTS
```

### 4. Instrucciones de conexion

Responde con:

```
Pipeline tooling lanzado en tmux. Para monitorear:
  tmux -CC attach -t tooling-<numero>

Usa /tooling-status para ver el progreso sin salir de aqui.
```

## Reglas

- **No esperes a que termine.** El script corre en background dentro de tmux. Devuelve el control inmediatamente.
- **No implementes nada tu mismo.** Solo lanza el script.
- Si tmux no esta instalado, el script lo detecta y muestra el error.
