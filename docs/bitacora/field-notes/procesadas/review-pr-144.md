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

---

## Segunda ronda de review — 2026-04-22

Despues del primer fix, el reviewer humano leyo el codigo corregido y detecto dos cambios de regla de dominio que no eran atrapables por agentes (requieren conocimiento del negocio, no de arquitectura).

### Comentarios

| # | Categoria | Resumen |
|---|---|---|
| 1 | corregir | El `throw` cuando `MinutosCompensados > MinutosRetardados` contradice el dominio: el retardo es un castigo; si la compensacion excede, el castigo satura en cero. El excedente va a la liquidacion de extras, fuera de este VO. |
| 2a | corregir | Los 2 tests de "lanza excepcion" se reemplazan por "RetardoNeto es cero" cuando compensacion excede retardo. |
| 2b | investigar | "Nadie externo al objeto debe acceder a los calculos intermedios o a los intervalos para hacer operaciones" -> privatizar datos crudos y exponer solo `RetardoNeto` y `ToString()`. Issue #147 creado para auditar que ningun caller esquive la encapsulacion. |

### Correcciones aplicadas

Commit `6255a27` en la rama del PR:

- `Crear()` ya no lanza — acumula suma cruda de ambas listas.
- `RetardoNeto = Math.Max(0, _minutosRetardados - _minutosCompensados)` — satura en cero.
- `TiempoRetardado`, `TiempoCompensado`, `MinutosRetardados`, `MinutosCompensados` migraron a privados.
- Interfaz publica: `Crear`, `Vacio`, `RetardoNeto`, `ToString()`, `Equals`, `GetHashCode`, `ConfigurarSerializacion`.
- `ToString()` expone los intervalos y totales (via `Mensajes` en .resx) para trazabilidad/auditoria.
- Tests reescritos para validar via `RetardoNeto` y `ToString()` (no acceden a privadas).
- Se elimino la clave `CompensadosExcedenRetardados` del .resx; se agregaron `SinRetardo`, `LabelRetardo`, `LabelCompensado`, `LabelNeto`.

Verificacion: 227 tests verdes en los 3 proyectos unitarios (Contracts 142 + ControlHoras 85). La reduccion vs la primera ronda se debe a que 5 tests que accedian a propiedades ahora privadas fueron absorbidos en tests de `ToString()`.

### Mejoras a agentes

Commit `2fdcb4d` en main:

| Agente | Gap | Ajuste |
|---|---|---|
| `planner.md` | Al listar propiedades en "Interfaz publica", nada obliga a distinguir "valor observable externamente" de "dato intermedio". El issue #114 v2 lista 4 propiedades publicas que deberian ser privadas porque el caller no las necesita — solo las usa `ToString()`. | Nota nueva en la plantilla: "Antes de listar una propiedad como publica, pregunta si es un valor observable externamente o un dato intermedio". Referencia ADR-0015 "Encapsulamiento: Tell Don't Ask". |

El resto de correcciones (eliminar `throw`, encapsular datos) fueron **decisiones de dominio** tomadas por el humano reviewer despues de leer el codigo generado. El pipeline no las habria atrapado porque seguia fielmente el issue v2. No hay regla tecnica atrapable automaticamente.

### Lecciones adicionales

- **Los issues evolucionan con el codigo**: incluso con DoR estricto, un issue puede cambiar despues de que el pipeline arranca — porque ver el codigo concreto revela matices del dominio que no eran obvios en la especificacion. El flujo `/fix-review` absorbe estas evoluciones en ciclos; no son fallas del pipeline.
- **El planner no reemplaza al dueño del dominio**: listar "Interfaz publica" en un issue es una hipotesis, no un veredicto. La nota agregada al planner recuerda cuestionar cada propiedad, pero la decision final sigue siendo humana y puede iterar.
- **Encapsulacion via `ToString()`**: cuando un VO tiene datos utiles para auditoria pero que nadie debe operar, exponerlos via `ToString()` es una alternativa limpia a propiedades publicas — el consumidor puede leer la representacion pero no puede iterar ni calcular sobre ella sin parsear el texto (friccion intencional).
