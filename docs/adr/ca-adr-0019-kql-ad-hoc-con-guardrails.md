# CA-ADR-0019: KQL ad-hoc con guardrails

**Fecha**: 2026-04-13
**Estado**: Aceptado

---

## Contexto

CA-ADR-0009 establecio un daily cap de 0.5GB en App Insights y restringio las consultas a 5 queries
KQL predefinidas en `scripts/appinsights-query.sh`. Esto fue una reaccion correcta al incidente
de $350 USD en 3 dias por logging excesivo.

Sin embargo, la experiencia con el agente `bug-investigator` ha mostrado que en ~10% de
investigaciones las queries predefinidas no contienen la informacion necesaria. El agente queda
ciego ante preguntas especificas (ej: "cuantos eventos ProgramacionTurnoDiarioSolicitada se
procesaron en la ultima hora?"). Esta limitacion degrada la capacidad diagnostica sin un beneficio
proporcional en control de costos -- una query ad-hoc con `take 20` y ventana de 1h tiene impacto
marginal vs el daily cap de 0.5GB.

Las 4 capas defensivas de CA-ADR-0009 (log levels, sampling, daily cap, alertas) siguen intactas.
Esta decision solo relaja la restriccion de "solo queries predefinidas".

## Decision

Se permite KQL ad-hoc a traves de un nuevo comando `custom` en `scripts/appinsights-query.sh`,
con guardrails automaticos inyectados por el script:

1. **Take forzado**: si la query no contiene `take` (case-insensitive), se inyecta `| take 20`
   al final. Esto limita el volumen de resultados devueltos.

2. **Ventana temporal forzada**: si la query no contiene `ago(`, se inyecta
   `| where timestamp > ago(1h)` como filtro. Las queries predefinidas usan 24h; la ventana
   reducida de 1h limita el volumen de datos escaneados.

3. **Warning visible**: se imprime `ADVERTENCIA: query ad-hoc. Daily cap: 0.5GB. Ver CA-ADR-0009.`
   en stderr antes de ejecutar. Esto es visible tanto para humanos como para agentes.

4. **Audit log**: cada ejecucion se registra en `scripts/.kql-audit.log` con timestamp y query
   final (con guardrails aplicados). El archivo esta en `.gitignore`.

El agente `bug-investigator` puede usar hasta 3 queries custom por sesion, solo en Stage 2
(Correlacion), cuando las queries predefinidas del Stage 1 no contengan la informacion necesaria.

## Consecuencias

**Positivas**

- El agente `bug-investigator` gana flexibilidad diagnostica en el ~10% de casos donde las
  queries predefinidas son insuficientes.
- Los guardrails automaticos evitan queries costosas sin depender de la disciplina del invocador.
- El audit log permite detectar patrones de uso excesivo o abuso.
- Las 4 capas defensivas de CA-ADR-0009 permanecen intactas.

**Negativas**

- Riesgo marginal de costo: una query con `take 20` y ventana de 1h escanea mas datos que cero,
  pero el impacto es despreciable frente al daily cap de 0.5GB.
- La inyeccion de `where timestamp > ago(1h)` es heuristica: si la query ya filtra por otro
  campo temporal, el filtro adicional es redundante pero no danino.
- El limite de 3 queries por sesion depende de la disciplina del agente (no esta enforceado
  por el script).
