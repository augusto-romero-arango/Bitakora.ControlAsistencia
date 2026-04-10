---
fecha: 2026-04-10
hora: 09:48
sesion: event-stormer
tema: Diseno del pipeline de debugging para investigacion de errores en entorno desplegado
---

## Contexto

El proyecto tiene pipelines automatizados para TDD, IaC y tooling, pero no existe un proceso sistematico para debugging. En sesiones recientes, investigar errores en App Insights requirio copiar-pegar stacktraces manualmente a Claude. El proceso funciono pero fue manual e irrepetible. Se busca cristalizar ese flujo en un pipeline reusable.

## Descubrimientos

### Acceso programatico a App Insights

- **Azure MCP Server oficial** (Microsoft, feb 2026): solo expone "list code optimization recommendations". NO ejecuta queries KQL. Insuficiente para debugging.
- **MCP de terceros** (`furoTmark/application-insights-mcp`): repo con README vacio, sin documentacion. `4R9UN/mcp-kql-server`: apunta a ADX clusters, no App Insights directamente.
- **`az monitor app-insights query`** de Azure CLI: funciona, esta mantenido, usa `az login`, permite queries KQL arbitrarias. Es la opcion viable hoy.

### Naturaleza del debugging vs otros pipelines

- El debugging es fundamentalmente **conversacional e interactivo**. El humano necesita validar hipotesis, dar contexto de negocio, y decidir si la causa raiz es correcta.
- Esto lo hace diferente a TDD (automatizable end-to-end) o tooling (semi-automatizable).
- Por lo tanto, un script bash con stages/worktree/tmux no es el formato correcto. Un agente de Claude es el formato natural.

### Queries KQL deben ser predefinidas, no generadas

- ADR-0009 establece un daily cap de 0.5 GB en dev. Queries KQL abiertas (generadas por el agente) podrian ser costosas o ineficientes.
- Queries predefinidas con `take N` y `ago(Xh)` son auditables, conservadoras, y suficientes para el 90% de los casos.

## Decisiones

### 1. Script wrapper `appinsights-query.sh` sobre az CLI (no MCP)
- Queries KQL predefinidas: exceptions, dead-letters, function-errors, traces (con filtro), health-summary
- Limites conservadores por defecto (24h, max 50 rows)
- Configuracion via .env local (app name, resource group)
- Prerequisito: `az login` activo

### 2. Patron skill + agente: `/bug` + `bug-investigator`
- Consistente con `/implement` + `implementer`, `/tooling` + agentes de tooling
- El skill valida prerequisitos y lanza el agente
- El agente hace el trabajo pesado: recoleccion, correlacion, diagnostico, accion

### 3. El agente NO modifica codigo de produccion
- Solo puede escribir en `docs/bitacora/field-notes/`
- El output son issues de GitHub (accionables por `/implement` o `/tooling`)
- Separacion clara: investigar != corregir

### 4. Stages del agente: Recoleccion -> Correlacion -> Diagnostico -> Accion
- Stage 1 (Recoleccion): queries automaticas a App Insights
- Stage 2 (Correlacion): lectura de codigo, ejecucion de smoke tests, WebSearch
- Stage 3 (Diagnostico): presentacion al usuario, validacion de hipotesis
- Stage 4 (Accion): creacion de issues, field notes

### 5. Field notes obligatorias (igual que event-stormer)
- Template adaptado con secciones: sintoma, investigacion, diagnostico, acciones

### 6. Integracion bidireccional con smoke tests
- Smoke test falla -> `/bug` investiga la causa en App Insights
- Bug investigation revela gap -> sugiere issue para smoke-test-writer

## Descartado

| Alternativa | Razon |
|---|---|
| Azure MCP Server oficial | Solo list recommendations, no KQL queries |
| MCP de terceros | Inmaduros, mal documentados |
| Pipeline bash con worktree/tmux | Debugging es conversacional, no automatizable e2e |
| Queries KQL generadas ad-hoc por el agente | Riesgo de costos, imposible auditar |
| Agente que modifique codigo | Viola separacion de responsabilidades |

## Preguntas abiertas

- **Service Bus dead letter queue**: se puede consultar directamente con `az servicebus topic subscription show` para contar mensajes en DLQ? Esto complementaria la vista de App Insights. Investigar en la sesion de implementacion.
- **Alertas como trigger**: las alertas de ADR-0009 (pico de excepciones >50 en 5min) podrian eventualmente disparar el pipeline de debugging automaticamente (via webhook -> GitHub Actions -> issue). Esto es futuro, no MVP.
- **Queries KQL custom**: si las predefinidas no son suficientes, se podria agregar un modo `--raw "query KQL"` con un warning explicito sobre costos. Decidir en la implementacion.

## Referencias

- ADR-0009: Control de costos de Application Insights
- ADR-0016: Smoke tests contra entorno dev
- Azure MCP Server docs: https://learn.microsoft.com/en-us/azure/developer/azure-mcp-server/tools/application-insights
- az CLI App Insights: https://learn.microsoft.com/en-us/cli/azure/monitor/app-insights
