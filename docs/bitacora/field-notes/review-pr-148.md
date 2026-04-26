# Field Note: Review del PR #148

**Fecha**: 2026-04-26
**PR**: https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/pull/148
**Issue**: #143 - Alinear IntervaloTemporal con ADR-0015 y agregar factories de conversion al VO

## Comentarios del review

| # | Categoria | Resumen |
|---|---|---|
| 1 | corregir | `IntervaloTemporal.Inicio` y `Fin` quedaron como propiedades publicas get-only sobre campos privados — es la misma fuga de datos que el patron anterior con mas codigo. Aplicar Tell-don't-Ask: investigar usos reales y exponer comportamiento, no datos. |
| 2 | corregir / explicar | Los nombres de tests del PR (`Desde_...`, `Partir_...`) no siguen `Debe_` que prescribe el agente test-writer. La realidad es que la codebase tiene 178 tests con `<Sujeto>_<LoQuePasa>_Cuando<Condicion>` y solo 60 con `Debe...`. Necesita ADR + ajuste de agente. |
| 3 | corregir | Inconsistencia entre la regla del agente y la convencion observada: hay que normalizar y documentar en ADR. |

## Correcciones aplicadas

### Comentario 1 — commit `3736e8c` en la rama del PR

`IntervaloTemporal` reescrito con Tell-don't-Ask:

- Eliminadas las propiedades publicas `Inicio` y `Fin`. Los momentos viven solo como campos privados readonly.
- Interfaz publica: `Crear()`, `Desde()`, `Partir()`, `DuracionEnMinutos`, `DuracionEnHorasDecimales`, `ResolverA()`, `ToString()`, igualdad por valor.
- `ConfigurarSerializacion` simplificado: ya no necesita el bucle que removia las propiedades auto-detectadas (no las hay). El registro manual de "Inicio" y "Fin" sobre los campos preserva la forma JSON original.
- Tests dependientes refactorizados a igualdad completa del intervalo y a `ToString()` / `ResolverA()` / `DuracionEnMinutos` para verificar comportamiento — no getters internos.
- Eliminados dos tests redundantes (`Inicio_EsElMomentoDeInicio`, `Fin_EsElMomentoDeFin`) absorbidos por la igualdad por valor del VO.

Verificacion: 282 tests verdes (Contracts 151 + ControlHoras 89 + Programacion 42).

### Comentarios 2 y 3 — commit `8d0ad44` en `main`

ADR-0022 nuevo: "Convencion de nombres para metodos de test". Establece patron unico `<Sujeto>_<LoQuePasa>[_Cuando<Condicion>]` con ejemplos por tipo (VOs, entidades, command handlers, validators, endpoints, smoke). Para command handlers el sujeto es el nombre del comando, no `HandleAsync` ni `Debe...`.

Alineacion de agentes y CLAUDE.md:

- `.claude/agents/test-writer.md`: regla y 4 ejemplos `Debe...` migrados al patron canonico.
- `.claude/agents/reviewer.md`: regla actualizada.
- `CLAUDE.md`: indice tematico de ADRs ahora incluye ADR-0022.

Issue #149 creado para migrar los 60 tests con prefijo `Debe...` al patron canonico.

## Mejoras a agentes

| Agente / Archivo | Gap | Ajuste aplicado |
|---|---|---|
| `test-writer.md:261` | Regla escrita (`Debe[Resultado]_Cuando[Condicion]`) que la mayor parte de la codebase nunca siguio. La regla genero ruido en revisiones humanas que aplicaron el patron real del codigo en lugar del prescrito. | Regla reformulada para apuntar a ADR-0022. Cuatro ejemplos migrados (`DebeEmitirPausaRegistrada` -> `NotificarPausa_EmitePausaRegistrada`, etc). |
| `reviewer.md:194` | Mismo gap: prescribia el patron `Debe[Resultado]_Cuando` que no era el dominante. | Regla reformulada para apuntar a ADR-0022. |
| `CLAUDE.md` indice de ADRs | No tenia entrada para naming de tests; era una omision dado que el ADR de testing (ADR-0006) cubre solo el DSL Given/When/Then. | Entrada agregada para ADR-0022. |
| Codebase | 60 tests con patron antiguo. | Issue #149 abierto para migracion mecanica. No bloquea este PR. |

## Lecciones

- **Una regla en un agente solo es util si coincide con la realidad**: la regla `Debe[Resultado]_Cuando[Condicion]` vivia en `test-writer.md:261` y `reviewer.md:194` desde antes, pero solo 60 de 238 tests la seguian. El reviewer humano la aplico al juzgar tests del PR — en buena fe — y genero un comentario que reflejaba el agente, no la codebase. La leccion para futuros ADRs/agentes: cuando se escribe una regla, **medir cuantos artefactos ya la cumplen**; si la mayoria no la cumple, el agente debe reflejar la realidad o levantar el debate explicito antes de prescribir.
- **"Encapsulamiento" no es exponer campos privados via propiedades publicas get-only**: el patron `private readonly T _x; public T X => _x;` es la misma superficie publica que `public T X { get; }`, con mas codigo y sin ganancia. Tell-don't-Ask exige preguntar primero "¿quien lee este dato y para que?" antes de exponerlo. En `IntervaloTemporal` la respuesta era "nadie en produccion, solo tests que verifican implementacion" — la fix correcta es eliminar la exposicion y refactorizar tests para verificar comportamiento.
- **Los tests tambien tienen que respetar Tell-don't-Ask**: dos tests del PR (`Inicio_EsElMomentoDeInicio`, `Fin_EsElMomentoDeFin`) verificaban literalmente "el getter retorna el dato que pase al ctor". Eso prueba un setter, no un comportamiento, y sirve de excusa para mantener publica la propiedad. La igualdad por valor del VO ya cubria el caso real. Cuando un test solo verifica un getter, suele ser senal de que el getter no deberia ser publico.
