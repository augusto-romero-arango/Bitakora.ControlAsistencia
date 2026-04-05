---
fecha: 2026-04-04
hora: 14:50
sesion: plan-mode
tema: Frustraciones con el agente event-stormer y diseño del pipeline de conocimiento del dominio
---

## Contexto

El usuario tuvo frustraciones concretas tras la primera sesion con el agente `event-stormer` (sesion de lenguaje ubicuo, 14:02 del mismo dia). Se iniciaron dos discusiones: una sobre las limitaciones del agente, y otra más profunda sobre qué artefactos necesita un sistema Event Sourcing para documentar su dominio de forma que alimente bien al pipeline TDD.

## Descubrimientos

### Limitaciones del agente event-stormer

- El agente `event-stormer` y el `planner` tenian allowlists de tools que excluian WebSearch y WebFetch. No podian investigar fuentes externas — solo respondian desde el entrenamiento.
- El unico artefacto del agente `event-stormer` eran las field notes (narrativa libre). El `planner` no tenia forma eficiente de consumir el conocimiento descubierto.
- `catalog.yaml` estaba definido en el eda-modeler pero solo cubria objetos tecnicos (eventos, comandos, policies, value objects). No habia espacio para actores, invariantes de aggregates, ni el mapa de relaciones entre contextos.

### Tecnicas investigadas (con fuentes externas)

Se investigo con WebSearch qué artefactos son esenciales para un sistema ES con un solo desarrollador. Fuentes: ddd-crew (Aggregate Design Canvas, Bounded Context Canvas), Alberto Brandolini (EventStorming.com), Oskar Dudycz (event-driven.io), Eric Evans.

**Event Storming por niveles**:
- Big Picture: produce domain events, actores (post-its amarillos), sistemas externos, pivotal events
- Process Modeling: agrega commands, policies, read models
- Design Level: agrega aggregates con invariantes

**Hallazgo clave**: el `eda-modeler` ya implementa el nivel de Process Modeling como YAML. Lo que faltaba eran artefactos para el lenguaje ubicuo estructurado, los aggregates con invariantes, y el context map.

**Overhead descartado para un solo dev**: AsyncAPI formal, Event Storming con post-its fisicos, Bounded Context Canvas V3 completo, Process Manager state diagrams.

### Los actores son ciudadanos de primera clase

En Event Storming, los actores (post-its amarillos) son centrales — se colocan junto al comando que disparan. En DDD y Design Thinking tambien. El glosario debia tener una seccion dedicada a actores con responsabilidades y frustraciones, no solo al vocabulario tecnico.

### Las invariantes viven en los aggregates, no en el codigo de eventos

Los records C# de eventos son datos puros — no documentan las reglas de negocio que el aggregate root verifica. Las invariantes ("no se puede depurar sin turno asignado", "un dia aprobado no puede volver a depurarse") necesitan su propio espacio de documentacion.

## Decisiones

- `docs/eda/` se convierte en el knowledge hub del dominio con 5 tipos de artefactos YAML
- El `ubiquitous-language.yaml` cubre tres dimensiones: terminos, actores (con responsabilidades y frustraciones), y sistemas externos. Tambien tiene una seccion de preguntas abiertas del dominio.
- El `context-map.yaml` documenta bounded contexts con el tipo de relacion DDD (Conformist, Customer-Supplier, Upstream-Downstream) y la semantica de cada conexion ("el turno viaja completo, no como ID")
- Cada aggregate tiene su propio archivo YAML con estado, invariantes, comandos y downstream policies
- El agente `event-stormer` recibe WebSearch/WebFetch y una Fase 3.5 obligatoria para actualizar los artefactos de dominio
- El agente `planner` recibe Write y lee todo `docs/eda/` antes de conversar
- El agente `eda-modeler` NO recibe WebSearch — investigar es rol exclusivo de `proyecto`
- -> creado ADR-0013: Pipeline de conocimiento del dominio

## Descartado

- Dar WebSearch al `eda-modeler` — el usuario lo rechazo explicitamente. La investigacion es rol del agente `event-stormer`, el eda-modeler solo modela lo ya descubierto.
- Artefactos en Markdown estructurado — menos parseable que YAML para los agentes
- Herramientas externas (Notion, Miro) — rompen el flujo de terminal y no son versionables con git
- Un solo artefacto monolitico — mejor separar por tipo para que cada agente actualice solo su parte

## Preguntas abiertas

1. El lenguaje ubicuo tenia la pregunta: con el vocabulario nuevo, ¿siguen siendo correctos los dominios del ADR-0001 (Marcaciones, Empleados, Liquidacion, Notificaciones)? Esta pregunta sigue abierta y es critica antes de implementar nuevos dominios.
2. ¿Cuando se invoca el eda-modeler por primera vez para modelar un flujo real? Tiene todo el tooling listo pero 0 flujos creados.
3. Las `open_questions` del glosario son ahora el backlog de exploracion — ¿como se priorizan para las proximas sesiones del agente `event-stormer`?

## Referencias

- ADR creado: 0013-pipeline-conocimiento-dominio.md
- ADRs consultados: 0001, 0002, 0004, 0005, 0006, 0010, 0011
- Artefactos creados: docs/eda/ (6 archivos YAML + 2 directorios)
- Agentes modificados: event-stormer (tools + instrucciones), planner (tools + stack de conocimiento)
- Field note relacionada: 2026-04-04-1402-lenguaje-ubicuo-exploracion-inicial.md (la que genero las frustraciones)
