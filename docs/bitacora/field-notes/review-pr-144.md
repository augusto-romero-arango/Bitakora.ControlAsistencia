# Field Note: Review del PR #144

**Fecha**: 2026-04-22
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/144
**Issue**: #114 — Crear enum Concepto y value objects primitivos del desglose

## Comentarios del review

| # | Categoria | Resumen |
|---|---|---|
| 1 | corregir | `[JsonConstructor]` en ctor privado de `DetalleRetardo` no funciona con Marten (ADR-0015 l.227-230). Migrar al patron canonico con ctor vacio + `FieldInfo.SetValue`. |
| 2 | corregir | `DetalleRetardo.ConfigurarSerializacion` no se registra en `ConfiguracionSerializacionControlHoras.ConfigurarResolver` — sin registro es codigo muerto. |
| 3 | corregir | `record` con `IReadOnlyList<T>` promete igualdad por valor que no cumple (ADR-0015 l.43-45). El reviewer del pipeline lo noto pero lo desestimo. Migrar a `sealed class` + `IEquatable<T>` manual con `SequenceEqual`. |
| 4 | corregir | Tests de round-trip con STJ vanilla pasan aunque el registro no exista. Usar `CrearOpcionesMarten()` + test "sin registro falla" (CA-10). |

## Correcciones aplicadas

Commit `52315a6` en `worktree-issue-114-crear-enum-concepto-y-value-objects-prim`:

- `DetalleRetardo` reescrito como `sealed partial class` con patron canonico ADR-0015.
- Registro agregado en `ConfiguracionSerializacionControlHoras.ConfigurarResolver`.
- `IEquatable<DetalleRetardo>` manual con `SequenceEqual` + `GetHashCode` combinando elementos.
- Tests de round-trip de `DetalleRetardo` e `IntervaloClasificado` movidos a `ControlHoras.Tests/Infraestructura/` ejercitando `CrearOpcionesMarten()`.
- Test CA-10 "sin registro falla" como barrera anti-regresion.
- `DetalleRetardoIgualdadTests` heredando `IgualdadTestBase<DetalleRetardo>` con casos que ejercitan `SequenceEqual`.

Verificacion: 272 tests verdes (Contracts 145 + ControlHoras 85 + Programacion 42).

## Mejoras a agentes

Commit `60e0e75` en main — consolidar ADRs como fuente unica de verdad:

| Agente / Archivo | Gap | Ajuste aplicado |
|---|---|---|
| `implementer.md` | Seccion "Modelado de objetos de dominio" duplicaba y **contradecia** ADR-0015 (decia "record con factory static" para VOs con invariantes; el ADR dice `sealed class`). | Eliminada la duplicacion. Reemplazada con referencia al ADR + lista de patrones fallidos comunes (anti-patterns detectados en reviews previos). |
| `reviewer.md` | Regla ignorada: el reviewer noto el `record` con `IReadOnlyList` pero lo desestimo ("el issue no pide tests de IEquatable"). No hay subseccion explicita de violaciones comunes que DEBE detectar. | Agregada subseccion "Violaciones de ADR-0015 a detectar activamente" con 5 antipatterns + instruccion explicita de no desestimar. |
| `test-writer.md` | Regla faltante: el helper inline de round-trip pasa aunque el registro en el dominio no exista. No habia test "sin registro falla" como barrera anti-regresion. | Reescrita seccion 6d: usar `ConfiguracionSerializacion{Dominio}.CrearOpcionesMarten()` en vez de helper inline. Test "sin registro falla" obligatorio. |
| `planner.md` (preexistente en main) | No se enumeraban ADRs aplicables en cada issue. | Seccion `## ADRs aplicables` obligatoria en cada issue. Anclaje contractual entre issue y arquitectura. |
| `CLAUDE.md` (preexistente en main) | Los agentes tenian reglas arquitectonicas dispersas que se desfasaban de los ADRs. | Declaracion explicita: "Los ADRs son la unica fuente de verdad. Los agentes no duplican sus reglas, las consultan". Indice tematico tema -> ADR. |

## Lecciones

- **Duplicar reglas en agentes genera drift**: el ADR-0015 estaba bien escrito pero los agentes tenian su propia version paralela (y desactualizada). Cuando hay dos fuentes de verdad, una se vuelve obsoleta silenciosamente. La fix estructural es el principio "los agentes consultan, no duplican" — no solo corregir este caso.
- **"El issue no lo pide" no es justificacion para desestimar una regla del ADR**: el reviewer vio el `record` con `IReadOnlyList` pero aplico criterio propio. Los ADRs son contratos del proyecto, no guias opcionales — aplican al tipo, no dependen de que el issue lo pida explicitamente. El reviewer ahora tiene instruccion explicita de no desestimar los 5 antipatterns comunes.
- **Los tests deben ejercitar el contrato de produccion, no un mundo paralelo**: tests de round-trip con STJ vanilla validaron que `[JsonConstructor]` "funcionaba" — en STJ vanilla. En Marten fallaban silenciosamente. El test "sin registro falla" es la barrera mas simple y efectiva contra regresiones del tipo "alguien borra la linea de registro".
