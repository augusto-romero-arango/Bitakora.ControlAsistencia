---
fecha: 2026-04-06
hora: 16:12
sesion: refactor-review
tema: Convenciones vertical slice - revision de refactor CrearTurno y actualizacion de agentes
---

## Contexto
Revision a profundidad del refactor manual que el usuario realizo sobre CrearTurno, reorganizando de estructura horizontal (Dominio/, Functions/) a vertical slice (feature folders). El objetivo era establecer las convenciones definitivas y propagarlas a los 4 agentes del pipeline TDD.

## Descubrimientos
- La colision de namespace vs tipo (carpeta `CrearTurno/` vs record `CrearTurno`) se resuelve con sufijo `Function` en el feature folder: `CrearTurnoFunction/`. Elimina la necesidad de aliases en usings.
- La clase del endpoint se renombra de `Endpoint` a `FunctionEndpoint`. El atributo pasa de `[Function(nameof(CrearTurno))]` a `[Function(nameof(FunctionEndpoint))]`.
- Las entities (AggregateRoots, eventos) son de dominio, no de funcion. Siempre viven en `Entities/` a nivel raiz del proyecto, nunca dentro de un feature folder.
- Los tests se separan por responsabilidad: un archivo por clase testeada (CommandHandlerTests, ValidatorTests, FunctionEndpointTests, EventoTests), aunque compartan factory methods.
- El handler y validator viven en subcarpeta `CommandHandler/` dentro del feature folder.
- El HealthCheck va en la raiz del proyecto de dominio, no en `Functions/`.

## Decisiones
- **Sufijo `Function`**: solo para HTTP triggers (evita colision namespace/tipo). ServiceBus triggers sin sufijo.
- **`FunctionEndpoint`** como nombre estandar de la clase del endpoint en todos los feature folders.
- **`Entities/` siempre raiz**: cuando se considero poner entities dentro del feature folder, el usuario corrigio — las entities son de dominio.
- **No actualizar CLAUDE.md**: las convenciones viven en los agentes, no en la documentacion central del proyecto.

## Cambios realizados
- `.claude/agents/implementer.md` — ejemplo de endpoint HTTP con FunctionEndpoint y try-catch, arbol de directorios vertical slice, convenciones de naming actualizadas
- `.claude/agents/test-writer.md` — exploracion sin Dominio/Functions/, nueva seccion 3b de ubicacion de tests, stubs con rutas vertical slice
- `.claude/agents/reviewer.md` — naming con FunctionEndpoint, seccion de organizacion vertical expandida (produccion + tests)
- `.claude/agents/domain-scaffolder.md` — sin carpeta Functions/, Entities/ en raiz, HealthCheck en raiz

## Pendiente
- El refactor del codigo real de CrearTurno (mover Entities/ y Eventos/ de dentro del feature folder a la raiz del dominio) no se ejecuto en esta sesion — solo se actualizaron los agentes con las convenciones deseadas.
