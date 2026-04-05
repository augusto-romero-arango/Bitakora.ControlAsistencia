---
fecha: 2026-04-04
hora: 06:53
sesion: plan-mode
tema: sistema de field notes y agentes event-stormer + historiador
---

## Contexto
El usuario queria un sistema para capturar el conocimiento que se genera en conversaciones de proyecto (dominio, arquitectura, diseño) y que no siempre se materializa en ADRs o codigo. La motivacion: la historia del proyecto se estaba construyendo solo desde git, pero habia mucho razonamiento y contexto que se perdia cuando la sesion terminaba. Se habia creado la bitacora (`docs/bitacora/`) el dia anterior; ahora se queria un mecanismo de alimentacion sistematico.

## Descubrimientos

**Sobre Claude Code plan mode:**
- Plan mode es el mismo modelo (Sonnet 4.6) con instrucciones adicionales en el system prompt + restriccion read-only a nivel de sistema. No hay magia bajo el capo.
- NO existen hooks `PostSession`, `OnSessionEnd` ni `OnPlanModeExit` en Claude Code. Solo `PostToolUse`.
- Los hooks `PostToolUse` reciben `tool_input` y `tool_result` via stdin (JSON), pero no el contenido de la conversacion.
- Un agente custom bien instruido puede igualar o superar plan mode nativo para casos especificos, porque puede tener contexto de dominio que plan mode generico no tiene.

**Sobre la separacion de responsabilidades:**
- La separacion "plan mode nativo para cosas rapidas / agente proyecto para sesiones significativas" resuelve el problema de forma elegante sin over-engineering.
- El usuario tenia miedo de que un agente custom fuera inferior a plan mode. Al aclarar que plan mode es "solo instrucciones bien escritas + restriccion de sistema", el miedo se disipo.

**Sobre el hook de ExitPlanMode:**
- El usuario pregunto si el hook generaria overhead en otros proyectos o en conversaciones no relacionadas. La respuesta es no: `.claude/settings.json` es local al proyecto y solo aplica cuando Claude trabaja en ese directorio. El hook solo imprime un `echo`, no crea archivos.

## Decisiones

- **Dos agentes nuevos**: `event-stormer` (facilitador de sesiones de descubrimiento con field notes obligatorias, opus) e `historiador` (genera bitacora diaria, sonnet).
- **Agente proyecto usa Write** restringido por instruccion a `.claude/plans/` y `docs/bitacora/field-notes/`. No tiene Edit para forzar que solo cree archivos nuevos.
- **Field notes son un output obligatorio del agente proyecto**: el agente no puede terminar sin escribirlas. La instruccion dice explicitamente "esta fase no es opcional".
- **Historiador es conversacional**: presenta un borrador antes de escribir y acepta contexto verbal del usuario que no esta en ningun archivo.
- **Hook en ExitPlanMode** como recordatorio pasivo (no como automatizacion): solo imprime texto, no crea archivos.
- **Planner actualizado** para escribir field notes al finalizar sesion via Bash.

## Descartado

- **Agente que reemplaza plan mode completamente**: descartado porque el usuario valora plan mode nativo para cosas rapidas. La separacion por tipo de sesion es mejor que el reemplazo total.
- **Skill `/bitacora`** para generacion rapida de bitacora: descartado en favor del agente historiador conversacional. El usuario prefiere poder dialogar con el agente antes de que escriba la entrada.
- **Hook que escribe field notes automaticamente**: descartado porque los hooks no tienen acceso al contenido de la conversacion, solo a metadata de tool calls. Imposible tecnico.
- **Script wrapper de plan mode** (`plan.sh`): descartado por perder la interactividad nativa de plan mode y agregar complejidad sin beneficio claro.

## Preguntas abiertas

- El agente `event-stormer` tiene Write sin restriccion de path a nivel de sistema (solo por instruccion). Evaluar en uso real si es suficiente o si hay que agregar algun mecanismo adicional.
- El historiador hace commit al final de la sesion. Definir si siempre hace push o solo commit y deja el push al usuario.

## Referencias
- Sin issues creados en esta sesion
- Sin ADRs (puede ser candidato a ADR si se quiere documentar la decision de field notes como practica del equipo)
