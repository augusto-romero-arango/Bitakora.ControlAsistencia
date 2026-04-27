---
fecha: 2026-04-26
hora: 12:16
sesion: meta-pipeline
tema: rebalance de autoridad entre planner y agentes del pipeline
---

## Contexto

El PR #148 (issue #143, "Alinear IntervaloTemporal con ADR-0015 y agregar factories de conversion al VO") quedo con label `bloqueado` aunque sus 299 tests pasaban en CI. El bloqueo era stale: el reviewer ya habia resuelto la causa eliminando un archivo de tests obsoleto. Pero el flujo revelo una falla mas profunda en como el planner y los agentes del pipeline se reparten autoridad.

Esta sesion fue conversacional, no de implementacion de feature. Producto: cuatro agentes editados (`planner`, `test-writer`, `implementer`, `reviewer`).

## Diagnostico del bloqueo del PR #148

Cadena de eventos:

1. **Planner** redacto el issue #143. En "Impacto en archivos / Modifica" listo `tests/Bitakora.ControlAsistencia.Contracts.Tests/.../IntervaloTemporalSerializacionTests.cs` con la instruccion "usar `CrearOpcionesMarten()` en vez de STJ vanilla". Y CA-5 lo respaldaba.

2. **Contradiccion arquitectonica no detectada**: `CrearOpcionesMarten()` vive en `Bitakora.ControlAsistencia.ControlHoras.Infraestructura`. El test esta en `Bitakora.ControlAsistencia.Contracts.Tests`. `Contracts.Tests` no puede depender de `ControlHoras` sin invertir dependencias del proyecto. La sugerencia era literalmente imposible. La "Revision de complejidad" no la atrapo.

3. **Test-writer** tenia regla absoluta "NUNCA modifiques tests existentes". Creo el test nuevo `IntervaloTemporalSerializacionMartenTests` en `ControlHoras.Tests/Infraestructura/` (ubicacion correcta) pero no toco el archivo viejo.

4. **Implementer** detecto que los 2 tests viejos no eran salvables sin violar CA-1, CA-2 o ADR-0015 (que proscribe `[JsonConstructor]` en ctor privado). Reporto bloqueo arquitectonico siguiendo su procedimiento (`blockage-report.md` con hipotesis, intentos, acciones requeridas).

5. **Reviewer** aplico la excepcion "bug en el uso del framework de testing" para eliminar el archivo obsoleto. Documento la accion. Tests en verde, PR mergeable. Pero el label `bloqueado` y el comentario quedaron pegados.

Todos los agentes actuaron correctamente segun sus reglas. El gap fue del planner.

## Decisiones

### Cambio de filosofia: mandato → sugerencia

El usuario (en sus palabras): _"Tengo la sensacion que el planner esta tomando muchas decisiones de diseno que seguramente no le competen. Prefiero que el test-writer tenga la sabiduria de tomar sus decisiones de diseno sin que el planner las imponga. Podriamos hacer que el planner de sugerencias segun lo que investigo, pero si el test-writer toma una decision distinta, que la documente."_

Reencuadre acordado:

- **Planner** investiga y **sugiere**. Las secciones "Impacto en archivos" e "Interfaz publica" cambian de especificacion a propuesta revisable. Se anade una nueva seccion opcional "Investigacion del planner" con precedentes, ADRs aplicados y alternativas — el contexto que los agentes del pipeline necesitan para juzgar las sugerencias con fundamento.
- **Pipeline** ejecuta y **juzga**. Cada agente (test-writer, implementer, reviewer) tiene autoridad explicita para desviarse de las sugerencias cuando su juicio tecnico difiere. La desviacion se documenta en una tabla "Desviaciones del plan del planner" en su resumen.
- **Bloqueo** queda reservado para situaciones donde el agente realmente no puede decidir (no para contradicciones que el agente puede resolver con criterio).

### Cambios concretos a los agentes

| Agente | Cambio |
|---|---|
| `planner.md` | Template del issue: "Interfaz publica propuesta", "Impacto esperado en archivos (sugerencia)", nueva "Investigacion del planner". Dos senales cualitativas nuevas en Revision de complejidad ("Coherencia de dependencias entre proyectos", "Decisiones de diseno delegables"). Dos casillas en checklist pre-listo. Segunda frase guia: "El planner investiga y sugiere; el pipeline ejecuta y juzga." |
| `test-writer.md` | Regla #2 suavizada: puede modificar/eliminar tests existentes si el issue lo pide. Regla #12 reformulada: "Interfaz publica propuesta" como sugerencia, no mandato. Nueva regla #19: autoridad para resolver contradicciones estructurales del plan. Plantilla de resumen extendida con bloque "Desviaciones del plan del planner". |
| `implementer.md` | Regla #1 clarificada con excepciones acotadas. Regla #10 extendida para incluir desviaciones del plan del planner. Guia previa al `blockage-report.md` para detectar contradicciones estructurales (caso PR #148) antes de escribir bloqueo. |
| `reviewer.md` | Excepcion de seccion 2b ampliada: "bugs de framework O contradicciones estructurales del plan". Plantilla "Resolucion de bloqueo heredado" para distinguir resolucion-exitosa de bloqueo-final. |

### Decision sobre el PR #148

El usuario decidio manejar el PR aparte. El plan de cambios no toco el PR, su label `bloqueado` ni el comentario stale. Diagnostico: el bloqueo del PR esta resuelto en codigo (reviewer elimino el test obsoleto); solo queda housekeeping de label y comentario.

## Descartado

- **Cambiar ADR-0014 (Definition of Ready)**: las nuevas casillas viven en el checklist pre-listo del planner, complementario al DoR. El ADR queda intacto.
- **Re-diseno profundo del pipeline TDD**: el cambio es filosofico (mandato → sugerencia), no estructural. Los roles rojo/verde/refactor permanecen.
- **Permitir que el implementer modifique tests para resolver contradicciones**: se mantuvo la prohibicion. La resolucion estructural corresponde al test-writer (fase roja, regla #19) o al reviewer (fase refactor, seccion 2b). El implementer reporta y sigue.

## Aprendizajes

- **Precedentes documentados como anclas**: el planner ahora cita el caso PR #148 en la senal "Coherencia de dependencias entre proyectos" del Revision de complejidad. Asi el aprendizaje queda anclado al texto del agente, no solo a esta field note.
- **Distincion test-writer vs implementer en resolver contradicciones**: si la contradiccion la atrapa el test-writer en fase roja, la resuelve el (regla #19). Si llega a fase verde, el implementer no debe resolverla — reporta para que el reviewer la cierre como parte del refactor. Esto preserva el invariante "el implementer no toca tests" salvo .resx.
- **El "bloqueo arquitectonico" como categoria distinta**: antes solo existia el bloqueo "no puedo hacer pasar el test" (5 intentos enfocados). Ahora se distingue del bloqueo "el test esta mal planteado dada la estructura del proyecto" — son diagnosticos diferentes y rutas de resolucion distintas.

## Preguntas abiertas

- ¿Hace falta una segunda iteracion para entrenar al planner con ejemplos de "Interfaz publica propuesta" bien marcada como revisable vs mal marcada como espec? Posible si vemos en proximos issues que se sigue imponiendo.
- ¿El `pr-sync` o el skill `/implement` debe limpiar automaticamente el label `bloqueado` cuando el reviewer documenta "Resolucion de bloqueo heredado"? Hoy queda manual.
- Field note sugiere considerar un futuro ADR sobre "autoridad de diseno entre planner y pipeline" si el patron se consolida.

## Referencias

- PR #148: <https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/148>
- Issue #143
- ADR-0015 (modelado de objetos de dominio) — la regla `[JsonConstructor]` proscrita que detono el bloqueo
- ADR-0014 (Definition of Ready) — complementario al checklist pre-listo del planner
- Plan: `~/.claude/plans/quiero-discutir-el-bloqueo-transient-firefly.md`

Issues creados: ninguno.
Issues cerrados: ninguno.
Archivos editados: 4 agentes (`planner.md`, `test-writer.md`, `implementer.md`, `reviewer.md`).
