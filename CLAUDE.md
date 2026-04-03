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
planner → crea issue → tdd-pipeline.sh → PR listo
```

1. Usa el agente `planner` para explorar ideas y crear el issue en GitHub
2. Ejecuta el pipeline TDD pasándole el número de issue

## Herramientas disponibles

| Herramienta | Cuándo usarla | Comando |
|---|---|---|
| `planner` | Pensar, explorar ideas, crear issues | `claude --agent planner` |
| `dev-workflow` | Implementar una tarea (ciclo completo) | `claude --agent dev-workflow` |
| `batch-workflow` | Implementar múltiples issues secuencialmente (con merge) | `claude --agent batch-workflow` |
| `parallel-workflow` | Implementar múltiples issues en paralelo (sin merge) | `claude --agent parallel-workflow` |
| `infra-workflow` | Implementar un cambio de infraestructura Azure | `claude --agent infra-workflow` |
| `infra-bootstrap` | Bootstrap del backend Terraform + pipeline IaC (primer despliegue de un ambiente) | `claude --agent infra-bootstrap` |
| `domain-scaffolder` | Crear un nuevo dominio completo (Function App + tests + Terraform + deploy workflow) | `claude --agent domain-scaffolder` |

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

### Visibilidad en tiempo real

Mientras corre el pipeline, puedes seguir los eventos de los agentes en otra terminal:

```bash
tail -f .claude/pipeline/events.log
```

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
# Via agente (recomendado)
claude --agent infra-workflow

# Directamente via script
./scripts/iac-pipeline.sh 42 --env dev
./scripts/iac-pipeline.sh 42 --env dev --auto-apply    # Omite confirmacion (solo dev)
./scripts/iac-pipeline.sh 42 --env dev --skip-apply    # Solo write + review, crea PR
```

Stages: **Write (HCL)** → **Review (plan)** → **Apply**

El MCP server de HashiCorp (`@hashicorp/terraform-mcp-server`) esta configurado en `.mcp.json`
y permite a los agentes consultar documentacion del registry de Terraform en tiempo real.

**Prerequisitos**: `brew install terraform`, `brew install azure-cli`, `az login`

### Estructura de infraestructura

```
infra/
  modules/          # Modulos reutilizables por tipo de recurso
  environments/     # Configuracion por ambiente (dev, staging, prod)
```

## Agentes del pipeline (internos)

Los siguientes agentes son invocados por el script — no los uses directamente:

| Agente | Rol | Fase TDD |
|---|---|---|
| `test-writer` | Escribe tests + stubs de compilación | TDD - Roja |
| `implementer` | Implementa para hacer pasar los tests | TDD - Verde |
| `reviewer` | Refactoriza y verifica cobertura | TDD - Refactor |
| `es-test-writer` | Escribe tests ES + stubs de compilación | ES TDD - Roja |
| `es-implementer` | Implementa lógica ES para pasar los tests | ES TDD - Verde |
| `es-reviewer` | Revisa patrones ES, refactoriza y verifica cobertura | ES TDD - Refactor |
| `infra-writer` | Escribe HCL y valida con terraform validate | IaC - Write |
| `infra-reviewer` | Revisa seguridad y ejecuta terraform plan | IaC - Review |
| `infra-applier` | Aplica el plan pre-generado | IaC - Apply |

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
