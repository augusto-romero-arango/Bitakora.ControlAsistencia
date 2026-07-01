# CLAUDE.md — Bitakora.ControlAsistencia

Instrucciones para Claude Code en este proyecto.

## Principios de respuesta

- Comunícate siempre en **español**.
- **Cita fuentes verificables** al afirmar una best practice o recomendación técnica — documentación oficial, libro, RFC, ADR del proyecto. Si es conocimiento general sin fuente, dilo explícitamente. Nunca presentes opinión como hecho verificado; ante la duda, pregunta al usuario si tiene una referencia que prefiera seguir.

## Proyecto

Sistema de control de asistencias y cálculo de horas según legislación laboral colombiana.

- **Stack**: .NET 10, C#, xUnit, AwesomeAssertions
- **Despliegue**: Azure Functions isolated worker, comunicación por eventos
- **Remote**: `https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia.git`

### Harness integrado

Este proyecto consume el plugin `mefisto@augusto-romero-arango-harness` desde el marketplace privado registrado en `.claude/settings.json`. Los skills, agentes, scripts y ADRs del marco arquitectónico vienen del plugin, no del repo del proyecto.

- **Repositorio del harness**: https://github.com/augusto-romero-arango/eda-evsourcing-azure-harness
- **Skills disponibles**: `/mefisto:implement`, `:scaffold`, `:infra`, `:tooling`, `:bug`, `:draft`, `:merge`, `:parallel`, `:sequential`, `:show-flow`, `:work-status`, `:health-check`, `:eraser-diagram`, `:fix-review`.
- **Actualizar**: `/plugin update mefisto`.

#### Setup para nuevos desarrolladores

El marketplace y el plugin ya están declarados en `.claude/settings.json` (commiteado al repo), así que la instalación es prácticamente automática:

1. **Acceso al repo del harness**: asegúrate de poder leer `augusto-romero-arango/eda-evsourcing-azure-harness`. Si es privado, autentica con `gh auth login` con permisos de lectura.
2. **Abre Claude Code en el repo**: detectará el marketplace y el plugin habilitado. Si no lo instala solo, corre `/plugin` y confirma la instalación de `mefisto`.
3. **Recarga** con `/reload-plugins` para activar skills y agentes sin reiniciar la sesión.

Para verificar que quedó: corre `/mefisto:health-check` o invoca cualquier skill `/mefisto:*` desde el prompt. Para traer cambios publicados en el harness: `/plugin update mefisto`.

### Tokens del harness (resolución para agentes y skills)

Estos valores los consumen los agentes/skills del harness cuando ven los placeholders `<RootNamespace>`, `<SolutionFile>`, `<ProjectDisplayName>`. La fuente operativa para scripts es `.claude/harness.config.json`.

- **RootNamespace**: `Bitakora.ControlAsistencia`
- **SolutionFile**: `ControlAsistencias.slnx`
- **ProjectDisplayName**: `ControlAsistencias`

### Estructura

- `src/Bitakora.ControlAsistencia.Contracts/` — contratos de eventos y value objects compartidos
- `src/Bitakora.ControlAsistencia.{Dominio}/` — Function App por dominio
- `tests/Bitakora.ControlAsistencia.{Dominio}.Tests/` — pruebas por dominio
- `infra/environments/{env}/` — infraestructura Terraform
- `docs/adr/` — decisiones arquitectónicas (ADRs)
- `docs/bitacora/` — bitácora y field notes
- `docs/eda/` — modelo de dominio (glosario, catálogo, flujos, aggregates)

## Comandos dotnet

```bash
dotnet build
dotnet test
dotnet test tests/Bitakora.ControlAsistencia.{Dominio}.Tests/
dotnet test --filter "NombreTest"
```

## Catálogo de skills

Los skills son comandos `/…` que orquestan trabajo. Cada uno documenta su propio uso; aquí solo los listo para que sepas cuál invocar.

| Skill | Propósito |
|---|---|
| `/draft` | Captura una idea como issue `estado:borrador`, sin fricción |
| `/implement` | Lanza el pipeline TDD para un issue `estado:listo` |
| `/tooling` | Pipeline de tooling (scripts, fixtures, config, agentes) — sin ciclo rojo/verde |
| `/infra` | Pipeline IaC con Terraform (write → review → apply) |
| `/parallel` | Corre varios issues en worktrees aislados sin merge automático |
| `/sequential` | Cadena de issues con merge automático entre PRs |
| `/scaffold` | Crea el scaffold de un nuevo dominio (proyecto + tests + Terraform + workflow) |
| `/bug` | Investiga un síntoma; enruta a `bug-investigator` o `tooling-investigator` |
| `/fix-review` | Resuelve comentarios pendientes de un PR en revisión |
| `/health-check` | Dashboard del entorno desplegado (excepciones, dead letters, requests) |
| `/work-status` | Progreso de los pipelines activos en tmux |
| `/show-flow` | Renderiza un flujo de `docs/eda/flows/` |
| `/eraser-diagram` | Genera diagrama para Eraser a partir de un flujo |

Flujo típico: `/draft` → planner (modo refinar) → `/implement` → PR → cierre del issue.

## Agentes disponibles

Invocables directamente con `claude --agent <nombre>` cuando necesites iterar fuera de un pipeline.

| Agente | Cuándo usarlo |
|---|---|
| `planner` | Knowledge crunching, crear/refinar/descartar issues, organizar backlog |
| `event-stormer` | Sesión de descubrimiento de dominio (genera field notes) |
| `eda-modeler` | Formaliza flujos y aggregates en `docs/eda/` |
| `historiador` | Consolida field notes en la bitácora del día |
| `domain-scaffolder` | Crea scaffold de un nuevo dominio (invocado por `/scaffold`) |
| `test-writer` | Fase roja del pipeline TDD (invocado por `/implement`) |
| `implementer` | Fase verde del pipeline TDD (invocado por `/implement`) |
| `reviewer` | Revisión antes de crear PR |
| `smoke-test-writer` | Smoke tests contra entorno dev |
| `infra-writer` / `infra-reviewer` / `infra-applier` / `infra-bootstrap` | Etapas del pipeline IaC |
| `pr-sync` | Integra PRs de un batch paralelo en el orden pedido |
| `bug-investigator` | Investiga errores del entorno desplegado (App Insights) |
| `tooling-investigator` | Investiga errores del tooling local (pipelines, skills, agentes) |

## Convenciones del proyecto

### Issues

- **Títulos**: `[verbo infinitivo] [qué cosa]` — sin prefijos (`EMP001`, `feat:`, `HU-`).
- **Labels obligatorios**: `tipo:X` + `dom:X` + `estado:{borrador|listo}`. Los asigna el planner.
- **Dependencias**: cada issue las declara en su sección `## Dependencias`. No se usan issues contenedor/epic.
- **Bloqueados**: label `bloqueado` cuando dependen de otro no cerrado. El pipeline lo quita al verificar dependencias.
- **Definition of Ready**: ver `docs/adr/0014-definition-of-ready.md` — los skills de pipeline lo validan antes de ejecutar.

Setup inicial de labels: `./scripts/setup-github-labels.sh`. Al crear un dominio nuevo, recuerda `gh label create dom:<nombre>`.

### Código

- **Caracteres prohibidos en archivos `.cs`**: nunca uses `─` (U+2500) ni otros caracteres decorativos Unicode. Solo guión ASCII `-` (U+002D). Aplica a comentarios, separadores y documentación inline.
- **Commits** en español, descriptivos, frecuentes.

### PRs y ramas

- Ramas de trabajo: `worktree-issue-<num>-<slug>` (los pipelines las crean).
- PRs deben incluir `Closes #<número>` (los pipelines lo hacen).

### ADRs

Cuando trabajes en una decisión arquitectónica (nueva estrategia de testing, cambio de patrones, adopción de librerías, cambios en comunicación entre dominios), evalúa si merece un ADR en `docs/adr/`. Si lo merece, propónlo como parte del plan. Formato: contexto, decisión, consecuencias.

**Los ADRs son la única fuente de verdad arquitectónica del proyecto.** Los agentes no duplican sus reglas: las consultan, las aplican y documentan cuando se desvían de ellas. El planner evidencia los ADRs aplicables en cada issue; implementer y reviewer los leen y verifican.

### Índice temático de ADRs

Si tu trabajo toca uno de estos temas, consulta el ADR correspondiente antes de tomar decisiones estructurales:

| Tema | ADR |
|---|---|
| **Encapsulamiento, Tell-don't-Ask, ocultación de estado interno (aplica por igual a aggregates y a value objects)** | ADR-0015 |
| Serialización, value objects con ctor privado, records vs sealed class, `ConfigurarSerializacion`, proscripción de `[JsonConstructor]` | ADR-0015 |
| Manejo de errores en event sourcing, eventos de fallo, no-throw en `Apply()` | ADR-0007 |
| Naming de eventos, versionado | ADR-0005 |
| Naming de funciones Azure (HTTP y Service Bus) | ADR-0008 |
| Topics y subscriptions de Service Bus, un topic por evento | ADR-0004 |
| Estrategia de testing con event sourcing, DSL de tests | ADR-0006 |
| Contracts: eventos públicos y value objects compartidos | ADR-0002 |
| Mensajes en `.resx` por aggregate/handler | ADR-0012 |
| Definition of Ready | ADR-0014 |
| Smoke tests contra entorno dev | ADR-0016 |
| Snapshots de Marten (excepción) | ADR-0021 |
| Convención de nombres para métodos de test (`<Sujeto>_<LoQuePasa>_Cuando<Condicion>`) | ADR-0022 |
| Archivo señal de refactor puro vive en `pipeline-state/` (fuera de `.claude/`) | ADR-0023 |
| Extracción vs duplicación, Rule of Three, evolución del código | ADR-0024 |
| El modelo de dominio rico vive en el dominio y no cruza el bus; Contracts solo DTOs planos | ADR-0025 |
| Custodia de secretos: connection strings en Key Vault, referencias `@Microsoft.KeyVault(...)`, `AzureWebJobsStorage` por identidad administrada | ADR-0026 |

Si una regla no aparece en ADRs pero la descubres repetida en varios lugares del proyecto, **propón un ADR** antes de replicarla en agentes.

## Notas para definir agentes y skills

- Las herramientas **MCP requieren declaración explícita** cuando un agente usa allowlist `tools:` en su frontmatter. Usa wildcard: `mcp__<servidor>__*` (ej: `mcp__terraform__*`, `mcp__jetbrains__*`).
- Si el agente **no** define `tools:`, hereda todas incluyendo MCP.
- Servidores MCP disponibles: `.mcp.json` (terraform) y plugin de JetBrains (solo con el IDE abierto).

## Fuentes de verdad

Cuando necesites entender cómo funciona algo, no asumas — lee:

- **Decisiones arquitectónicas**: `docs/adr/`
- **Detalle de un skill**: `.claude/commands/<skill>.md`
- **Detalle de un agente**: `.claude/agents/<agente>.md`
- **Detalle de un pipeline**: `scripts/<pipeline>.sh`
- **Modelo de dominio**: `docs/eda/` (glosario, catálogo, flujos, aggregates)
- **Bitácora**: `docs/bitacora/`

## Arquitectura objetivo

- Azure Functions serverless por dominio.
- Comunicación exclusiva por eventos (Service Bus). Sin llamadas directas entre funciones.
- La verdad viaja en el evento. Cada función es autónoma.
- Estado base en `infra/environments/dev/` con Terraform.
