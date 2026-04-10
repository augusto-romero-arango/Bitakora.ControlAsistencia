# CLAUDE.md — Bitakora.ControlAsistencia

Instrucciones para Claude Code en este proyecto. Comunícate siempre en **español**.

## Proyecto

Sistema de control de asistencias y cálculo de horas según legislación laboral colombiana.

- **Stack**: .NET 10, C#, xUnit, AwesomeAssertions
- **Remote**: `https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia.git`
- **Estructura**:
  - `src/Bitakora.ControlAsistencia.Contracts/` — contratos de eventos y value objects compartidos
  - `src/Bitakora.ControlAsistencia.{Dominio}/` — Function App por dominio (isolated worker)
  - `tests/Bitakora.ControlAsistencia.{Dominio}.Tests/` — pruebas del dominio correspondiente
  - `docs/adr/` — decisiones arquitectónicas (ADRs)

## Comandos principales

```bash
dotnet build                                                      # compilar toda la solucion
dotnet test                                                       # correr todas las pruebas
dotnet test tests/Bitakora.ControlAsistencia.{Dominio}.Tests/    # correr pruebas de un dominio
dotnet test --filter "NombreTest"                                 # correr una prueba especifica
```

## Flujo de trabajo

```
/draft "idea"  →  estado:borrador
planner modo 7 →  refina → estado:listo
/implement 42  →  pipeline TDD → PR → cierra issue
/tooling 18    →  pipeline tooling → PR → cierra issue
/infra 42      →  pipeline IaC → PR → cierra issue
/bug "sintoma" →  investiga → issue(s) estado:listo
```

1. Captura ideas rápidas con `/draft [idea]` — crea un issue borrador sin fricción
2. Usa el agente `planner` para refinar borradores (modo 7) o crear issues completos directamente
3. Lanza el pipeline TDD con `/implement <numero>` — corre en tmux, no bloquea tu terminal
4. Lanza el pipeline tooling con `/tooling <numero>` — para tareas sin ciclo TDD (scripts, fixtures, config)
5. Lanza el pipeline IaC con `/infra <numero>` — corre en tmux, worktree aislado, default env=dev
6. Investiga errores en producción con `/bug [sintoma]` — consulta App Insights, correlaciona con código y crea issues

### Bitácora y field notes

El proyecto mantiene una bitácora en `docs/bitacora/` con una entrada por día de trabajo.

- **Sesiones de descubrimiento** (dominio, arquitectura, diseño): usar `claude --agent event-stormer` — genera field notes obligatorias al cerrar
- **Sesiones del planner**: generan field notes automáticamente al finalizar
- **Al cerrar el día**: `claude --agent historiador` — lee field notes + git + issues y produce la entrada de bitácora
- Las field notes crudas van a `docs/bitacora/field-notes/`, las procesadas a `field-notes/procesadas/`

### Gestión de issues

- **Titles**: `[verbo infinitivo] [qué cosa]` — sin prefijos (EMP001, feat:, HU-)
- **Labels obligatorios**: `tipo:X` + `dom:X` + `estado:listo` (el planner los asigna)
- **Dependencias**: cada issue declara sus dependencias en la sección `## Dependencias` — no se usan issues contenedor/epic
- **Bloqueados**: label `bloqueado` en issues que dependen de otro no cerrado
- **Nuevos dominios**: al crear con `domain-scaffolder`, agregar `dom:X` con `gh label create`

Setup inicial de labels: `./scripts/setup-github-labels.sh`

## Pipeline TDD

El script `scripts/tdd-pipeline.sh` ejecuta el ciclo completo de forma autónoma:

```
Issue → Worktree → Tests (rojo) → Implementación (verde) → Refactor → PR → Cleanup
```

```bash
# Desde un issue de GitHub (recomendado)
./scripts/tdd-pipeline.sh 42
./scripts/tdd-pipeline.sh --issue 42

# Desde un archivo markdown local
./scripts/tdd-pipeline.sh --file "docs/Historias de usuario/HU-25.md"
```

**El script crea su propio worktree, hace los commits, crea el PR y limpia el worktree.**
Si algo falla, el worktree queda disponible para inspección y el log de error muestra la causa.

## Pipeline paralelo

Para ejecutar múltiples issues simultáneamente sin merge automático:

```bash
./scripts/parallel-pipeline.sh 42 43 44
./scripts/parallel-pipeline.sh 42 43 44 --max-parallel 2   # limitar concurrencia
```

Cada issue corre en su propio worktree aislado. Al terminar, los PRs se crean pero no se mergean — usa `pr-sync.sh` después para integrar en el orden deseado.

## Pipeline IaC

Para crear o modificar infraestructura Azure con Terraform:

```bash
# Via skill (recomendado)
/infra 42

# Directamente via script
./scripts/iac-pipeline.sh 42                          # env=dev por defecto
./scripts/iac-pipeline.sh 42 --env dev --auto-apply   # Omite confirmacion (solo dev)
./scripts/iac-pipeline.sh 42 --skip-apply             # Solo write + review, crea PR
./scripts/iac-pipeline.sh 42 --from-stage 2           # Retomar desde Stage 2
```

Stages: **Write (HCL)** → **Review (plan)** → **Apply**

**El script crea su propio worktree, hace los commits, crea el PR y limpia el worktree.**
Si algo falla, el worktree queda disponible para inspección y el log de error muestra la causa.

## Pipeline Tooling

Para tareas que no son lógica de dominio (scripts, fixtures de test, configuración, agentes, skills):

```bash
# Via skill (recomendado)
/tooling 18

# Directamente via script
./scripts/tooling-pipeline.sh 18
./scripts/tooling-pipeline.sh 18 --from-stage 2   # Retomar desde Stage 2
```

Stages: **Writer (implementación)** → **Reviewer (revisión)** → **PR**

A diferencia del pipeline TDD, no tiene fases roja/verde. Los gates son compilación y que los tests existentes sigan pasando.

## Pipeline de debugging

Para investigar errores en el entorno desplegado:

```bash
# Via skill (recomendado)
/bug "las funciones de liquidacion estan fallando con NullReferenceException"

# Via agente directamente
claude --agent bug-investigator "sintoma aqui"
```

Stages: **Recolección (App Insights)** → **Correlación (código + fuentes)** → **Diagnóstico (hipótesis)** → **Acción (issues)**

El agente solo puede escribir en `docs/bitacora/field-notes/` — no modifica código. Requiere `az login` activo y el script `scripts/appinsights-query.sh`.

## Definición de agentes y skills

Al crear o modificar archivos en `.claude/agents/` o `.claude/skills/`:

- **MCP tools requieren declaración explícita**: si un agente tiene `tools:` en su frontmatter (allowlist), las herramientas MCP **no** se heredan automáticamente. Hay que declararlas con wildcard: `mcp__<servidor>__*` (ej: `mcp__jetbrains__*`, `mcp__terraform__*`).
- Si el agente **no** define `tools:`, hereda todas las herramientas incluyendo MCP.
- Servidores MCP disponibles: `.mcp.json` en la raíz del proyecto (terraform) y el plugin de JetBrains IDE (rider — disponible solo cuando el IDE está abierto).

## Estándares

- Los PRs deben incluir `Closes #<número>` en la descripción (el pipeline lo hace automáticamente)
- Las ramas de trabajo se nombran `worktree-issue-<num>-<slug>`
- Commits frecuentes con mensajes descriptivos en español
- Convenciones de tests: ver archivos existentes en `tests/Bitakora.ControlAsistencia.Contracts.Tests/`
- **Caracteres prohibidos en código**: NUNCA uses el carácter "─" (U+2500, box drawing) ni otros caracteres decorativos Unicode para líneas o separadores. Usa siempre el guión ASCII estándar "-" (U+002D). Esto aplica a comentarios, separadores, documentación inline y cualquier texto dentro de archivos `.cs`.
- **ADRs en tareas de arquitectura**: cuando estés en plan mode trabajando en una tarea que involucre decisiones arquitectónicas (nueva estrategia de testing, cambio de patrones, adopción de librerías, cambios en la comunicación entre dominios, etc.), evalúa si la decisión merece un ADR en `docs/adr/`. Si lo merece, propón su creación como parte del plan. Los ADRs siguen el formato: contexto, decisión, consecuencias.

## Arquitectura objetivo

- **Despliegue**: Azure Functions (serverless)
- **Comunicación**: por eventos (sin llamadas directas entre funciones)
- **Principio**: la verdad viaja en el evento. Cada función es autónoma.
- **Estado**: infraestructura base implementada en `infra/environments/dev/` con Terraform.
