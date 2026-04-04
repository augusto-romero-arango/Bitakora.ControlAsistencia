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

### 1.5. Validar Definition of Ready

Aplica la validacion programatica definida en la seccion "Validacion en `/implement`" del ADR `docs/adr/0014-definition-of-ready.md`.

Extrae labels y body del issue:

```bash
gh issue view $ARGUMENTS --json labels,body
```

Determina el tipo del issue buscando el label `tipo:X`. Luego verifica los 5 criterios del ADR-0014 y acumula todos los fallos antes de reportar.

Si **uno o mas criterios fallan**: muestra la lista completa de lo que falta, sugiere `claude --agent planner` en modo `refinar` para completarlos, y **detente**.

Si **todos los criterios pasan**: continua al paso 2.

### 2. Detectar dominio

Extrae el label `dom:X` del issue:

```bash
gh issue view $ARGUMENTS --json labels -q '[.labels[].name | select(startswith("dom:"))] | first // empty' | sed 's/^dom://'
```

- Si el resultado esta vacio (no hay label `dom:*`): establece `DOMINIO_KEBAB=""` y salta al paso 4.
- Si hay dominio: conviertelo a PascalCase (ej: `liquidacion-nomina` → `LiquidacionNomina`) y verifica si el proyecto existe:

```bash
test -d "src/Bitakora.ControlAsistencia.{PascalCase}/"
```

- Si el directorio existe: salta al paso 4.
- Si NO existe: continua al paso 3.

### 3. Confirmar scaffold del dominio (solo si no existe)

Muestra al usuario exactamente lo que se va a crear y pregunta de forma explicita:

```
El dominio "{kebab}" no tiene proyecto aun.
Se necesita crear el scaffold antes de lanzar el pipeline:
  - Function App:  src/Bitakora.ControlAsistencia.{PascalCase}/
  - Tests:         tests/Bitakora.ControlAsistencia.{PascalCase}.Tests/
  - Terraform:     infra/environments/dev/main.tf (storage + function app)
  - Workflow:      .github/workflows/deploy-{kebab}.yml

El scaffold se hara en el mismo worktree del issue — el PR incluira ambos.
¿Creo el dominio antes de lanzar el pipeline? (s/n)
```

**Si el usuario dice no**: responde que no es posible continuar sin el proyecto del dominio y detente.

**Si el usuario dice si**: establece `SCAFFOLD_FLAG="--scaffold-domain {kebab}"` y continua al paso 4.

### 4. Mostrar info y lanzar

Muestra una linea con el issue:

```
#42: Implementar calculo de horas extras nocturnas
Dominio: Liquidacion | Tipo: feature | Estado: listo
```

Si se hara scaffold, agrega:

```
Scaffold del dominio "{kebab}" incluido en el pipeline (Stage 0 antes de TDD).
```

Luego lanza el pipeline en tmux:

```bash
# Sin scaffold nuevo:
./scripts/tmux-pipeline.sh $ARGUMENTS

# Con scaffold:
./scripts/tmux-pipeline.sh $ARGUMENTS --scaffold-domain {kebab}
```

### 5. Instrucciones de conexion

Responde con:

```
Pipeline lanzado en tmux. Para monitorear:
  tmux -CC attach -t tdd-<numero>

Usa /pipeline-status para ver el progreso sin salir de aqui.
```

## Reglas

- **No esperes a que termine.** El script corre en background dentro de tmux. Devuelve el control inmediatamente.
- **No implementes nada tu mismo.** Solo lanza el script.
- **Nunca crees un dominio sin confirmacion explicita del usuario.** La creacion implica Terraform e infraestructura en Azure.
- El scaffold se ejecuta dentro del worktree del issue (Stage 0), no en main. Todo va en un solo PR.
- Si tmux no esta instalado, el script lo detecta y muestra el error. No intentes instalarlo.
