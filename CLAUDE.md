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
- **Actualizar**: `/plugin update mefisto`.
- **Setup para nuevos desarrolladores**: ver `docs/setup-desarrolladores.md`.

### Tokens del harness (resolución para agentes y skills)

Estos valores los consumen los agentes/skills del harness cuando ven los placeholders `<RootNamespace>`, `<SolutionFile>`, `<ProjectDisplayName>`. La fuente operativa para scripts es `.claude/harness.config.json`.

- **RootNamespace**: `Bitakora.ControlAsistencia`
- **SolutionFile**: `ControlAsistencias.slnx`
- **ProjectDisplayName**: `ControlAsistencias`

### Estructura

- `src/Bitakora.ControlAsistencia.PublicEvents/` — eventos que salen del bounded context y su payload (futuro NuGet, sin dependencias de proyecto)
- `src/Bitakora.ControlAsistencia.PrivateEvents/` — eventos del bus interno del BC y su payload
- `src/Bitakora.ControlAsistencia.{Dominio}.DomainEvents/` — eventos que se persisten en el event store de ese dominio, con su payload rico
- `src/Bitakora.ControlAsistencia.{Dominio}/` — Function App por dominio
- `src/Bitakora.ControlAsistencia.Projections/` — worker de proyecciones read-side
- `src/Bitakora.ControlAsistencia.ReadModels/` — read models planos, sin Marten
- `tests/Bitakora.ControlAsistencia.{Dominio}.Tests/` — pruebas por dominio
- `tests/Bitakora.ControlAsistencia.{Public|Private}Events.Tests/` — pruebas de cada ensamblado de eventos
- `infra/environments/{env}/` — infraestructura Terraform
- `docs/adr/` — decisiones arquitectónicas (ADRs)
- `docs/bitacora/` — bitácora y field notes
- `docs/ddd/` — glosario de lenguaje ubicuo (`ubiquitous-language.yaml`)
- `docs/eda/` — modelo de dominio (catálogo, flujos, aggregates)

## Skills y agentes

Los skills (`/mefisto:*`) y los agentes vienen del plugin y se autodescriben; el detalle de cada uno vive en `.claude/commands/<skill>.md` y `.claude/agents/<agente>.md`. Los agentes son invocables directamente con `claude --agent <nombre>` cuando necesites iterar fuera de un pipeline.

Flujo típico: `/draft` → planner (modo refinar) → `/implement` → PR → cierre del issue.

## Convenciones del proyecto

### Issues

- **Títulos**: `[verbo infinitivo] [qué cosa]` — sin prefijos (`EMP001`, `feat:`, `HU-`).
- **Labels obligatorios**: `tipo:X` + `dom:X` + `estado:{borrador|listo}`. Los asigna el planner.
- **Dependencias**: cada issue las declara en su sección `## Dependencias`. No se usan issues contenedor/epic.
- **Bloqueados**: label `bloqueado` cuando dependen de otro no cerrado. El pipeline lo quita al verificar dependencias.
- **Definition of Ready**: ver MEF-ADR-0011 (Definition of Ready, del marco) — los skills de pipeline lo validan antes de ejecutar.

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
| **Encapsulamiento, Tell-don't-Ask, ocultación de estado interno (aplica por igual a aggregates y a value objects)** | MEF-ADR-0012 |
| Serialización, value objects con ctor privado, records vs sealed class, `ConfigurarSerializacion`, proscripción de `[JsonConstructor]` | MEF-ADR-0012 |
| Manejo de errores en event sourcing, eventos de fallo, no-throw en `Apply()` | MEF-ADR-0004 |
| **Violación de regla de negocio en un comando HTTP sin consumidores downstream: el aggregate declina con resultado (no lanza, no emite evento de fallo) y el handler traduce a 409/404 con `.resx`; los eventos de fallo persistidos se reservan para flujos con un consumidor que reaccione** | CA-ADR-0030 (precisa el criterio capa 2 vs capa 3 de MEF-ADR-0004) |
| Naming de eventos, versionado | MEF-ADR-0005 |
| Naming de funciones Azure (HTTP y Service Bus) | MEF-ADR-0006 |
| Topics y subscriptions de Service Bus, un topic por evento | MEF-ADR-0001 |
| Estrategia de testing con event sourcing, DSL de tests | MEF-ADR-0002 |
| **Dónde va un evento nuevo: `PublicEvents` vs `PrivateEvents` vs `{Dominio}.DomainEvents`; qué referencia el worker de proyecciones; por qué un evento no conoce su comando; cero referencias entre los tres ensamblados (tres islas); payload por rol** | CA-ADR-0029 (aplicación local), MEF-ADR-0039 (canon del marco) |
| **Identidad de aggregates dentro de un store compartido: colisión de PK entre vecinos del mismo schema, heurística de anatomía de clave (prefijo + componentes + separador), registro de anatomías por store** | CA-ADR-0031 (precisa MEF-ADR-0037 sección 2 con la lectura "unidad = componente tipado"; propuesto al marco como harness#682) |
| **Identidad del evento en el event store: el alias manda, registro explícito con `AddEventTypes`, mover un evento de namespace sin migrar datos; proscripción de `MapEventType` y de alterar `EventNamingStyle`** | CA-ADR-0029 (decisión #6) |
| Mensajes en `.resx` por aggregate/handler | MEF-ADR-0009 |
| Definition of Ready | MEF-ADR-0011 |
| Smoke tests contra entorno dev | MEF-ADR-0013 |
| Worker de proyecciones read-side, proyecciones Marten (read models), Functions HTTP GET de consulta | MEF-ADR-0034, MEF-ADR-0035 |
| Snapshots de Marten (excepción) | MEF-ADR-0015 |
| Convención de nombres para métodos de test (`<Sujeto>_<LoQuePasa>_Cuando<Condicion>`) | MEF-ADR-0016 |
| Archivo señal de refactor puro vive en `pipeline-state/` (fuera de `.claude/`) | MEF-ADR-0017 |
| Extracción vs duplicación, Rule of Three, evolución del código | MEF-ADR-0018 |
| El modelo de dominio rico vive en el dominio y no cruza el bus; lo que cruza es plano | CA-ADR-0025 |
| Custodia de secretos: connection strings en Key Vault, referencias `@Microsoft.KeyVault(...)`, `AzureWebJobsStorage` por identidad administrada | CA-ADR-0026 |
| Tenancy conjoined operando con un unico tenant, `ITenantResolver` de valores fijos | CA-ADR-0027 |
| ~~Biblioteca `{Dominio}.Dominio` como frontera write/read~~ — superado por CA-ADR-0029; se conserva su prohibición de que el worker referencie un Function App | CA-ADR-0028 |
| ~~`Contracts` para eventos públicos y value objects compartidos~~ — superado por CA-ADR-0029; el proyecto fue eliminado | CA-ADR-0002 |

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
- **Glosario de lenguaje ubicuo**: `docs/ddd/ubiquitous-language.yaml`
- **Modelo de dominio**: `docs/eda/` (catálogo, flujos, aggregates)
- **Bitácora**: `docs/bitacora/`

## Arquitectura objetivo

- Azure Functions serverless por dominio.
- Comunicación exclusiva por eventos (Service Bus). Sin llamadas directas entre funciones.
- La verdad viaja en el evento. Cada función es autónoma.
- Estado base en `infra/environments/dev/` con Terraform.
