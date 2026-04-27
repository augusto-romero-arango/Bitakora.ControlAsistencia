---
fecha: 2026-04-26
hora: 21:15
sesion: tooling-investigator
tema: Stage 4 (Coverage Gate) — separar el bug del filtro de las degradaciones legitimas a `skipped`. Verificar la intencion del diseño antes de proponer fix.
---

## Sintoma reportado

Continuacion de la investigacion previa (`2026-04-26-2107-tooling-investigation.md`). El usuario pidio verificar antes del fix si el `skipped` del Stage 4 estaba diseñado a proposito para no bloquear el workflow en escenarios como PRs de smoke tests, y separar el bug real de las degradaciones legitimas.

> "Creo que la intencion del skipped era evitar que se bloqueara el workflow por culpa de la ejecucion de los smoke tests, que en ese punto no se deben verificar. Entonces verifiquemos que no estemos cometiendo un error y ten todo el panorama antes de proponer la correccion."

## Investigacion

### Mapa completo del Stage 4

Stage 4 (`scripts/tdd-pipeline.sh:947-1514`) tiene 8 caminos de salida. Resumen:

| Resultado | Linea | Condicion | Categoria |
|---|---|---|---|
| `skipped` (A) | 1513 | `IS_REFACTOR=true` o `FROM_STAGE>4` | Legitimo (refactor / reanudacion) |
| `skipped` (B) | 963 | `dotnet-coverage` no instalado | Legitimo (degradacion de tooling) |
| `skipped` (C) | 974 | `PR_SRC_FILES` vacio (sin `.cs` en `src/`) | Legitimo (PR de docs/infra/smoke) |
| `passed` (D) | 1086 | `LOGIC_FILES` vacio tras clasificacion | Legitimo (gate trivial) |
| `skipped` (E) | 1200 | `measure_coverage` retorno != 0 | Mezcla: bug del filtro + casos legitimos |
| `passed` (F) | 1497 | `COV_GAPS_REMAINING == 0` | Camino feliz |
| `gaps` (G) | 1493 | Gaps tras remediacion | Camino con alerta |

El camino problematico es E. Es un `or-soup` que enmascara 4 causas distintas de fallo (build, instrumentacion 0 por filtro, collect, XML ausente) bajo un mismo warning generico.

### ADR-0018 — fuente de verdad del Stage 4

`docs/adr/0018-coverage-gate-pipeline-tdd.md` define explicitamente la filosofia no-bloqueante:

- Linea 83: "El gate **nunca bloquea la creacion del PR**. Los gaps se reportan, no se imponen."
- Lineas 86-87: "Si la instrumentacion falla por cualquier razon, el pipeline emite warning y continua sin el coverage gate. Un fallo de tooling nunca debe bloquear el flujo de desarrollo."

Convertir el `skipped` del camino E en `failed` violaria el ADR.

### La intencion del filtro `[[ "$dll" == *Tests* ]]` no era proteger smoke tests

Verificacion empirica del glob:

```
"$WORKTREE_PATH"/tests/Bitakora.ControlAsistencia.*.Tests/bin/Debug/net10.0/Bitakora.ControlAsistencia.*.dll
```

solo expande dentro de `Contracts.Tests/`, `ControlHoras.Tests/`, `Programacion.Tests/`. **Nunca toca `*.SmokeTests/`** porque el patron `*.Tests/` requiere que el segmento de directorio termine exactamente en `Tests`. La proteccion contra smoke tests vive en otros niveles:

- Stage 2b condicional (linea 731-794): dispara smoke-test-writer solo si hay `Function/` modificado.
- `run_tests_projects()` (lineas 121-153, commit `e7a7f2c`): glob `tests/Bitakora.ControlAsistencia.*.Tests/` excluye SmokeTests.
- ADR-0016: smoke tests corren post-deploy, no en CI ni en pipeline TDD.
- Stage 4 - `PR_SRC_FILES` (linea 969): mira solo `src/*.cs`. Un PR de solo smoke tests entra al camino C (skipped legitimo).
- Stage 4 - bucle de DLLs: el glob ya excluye `*.SmokeTests/bin/`.

Conclusion: la intencion del filtro `[[ "$dll" == *Tests* ]]` era excluir las DLLs `Bitakora.ControlAsistencia.X.Tests.dll` que viven JUNTO a las DLLs de produccion en cada `tests/X.Tests/bin/Debug/net10.0/` (por copia de ProjectReference). El bug es que el patron operando contra la ruta absoluta matchea siempre porque `.Tests` aparece en el directorio padre.

### Historia confirma: bug nacio con el feature

`git log -S "instrumented" -- scripts/tdd-pipeline.sh` revela que el bucle solo aparece en `462cea8` (Stage 4 inicial, 2026-04-11). Los commits posteriores (`bb09c09`, `e7a7f2c`, `7d2fdb8`) no tocaron el bucle. **El bug ha estado activo 15 dias.**

### Evidencia historica del impacto

Inspeccion de 10 logs de pipeline TDD desde 2026-04-21:

- 7 corridas: tomaron el atajo D (passed trivial — `LOGIC_FILES` vacio). El bucle nunca se ejercito.
- 3 corridas con `LOGIC_FILES` no vacios (2026-04-21 15:59 issue #106; 2026-04-21 20:54; 2026-04-26 20:11 issue #108): **TODAS** cayeron en `"No se instrumento ninguna DLL"`. **Ningun log en el repo registra "DLL(s) instrumentada(s)".** El gate nunca ha medido cobertura desde su introduccion.

## Diagnostico

Hipotesis previa confirmada: bug del filtro contra ruta absoluta. La hipotesis del usuario sobre intencionalidad respecto a smoke tests **se descarta con evidencia**: la proteccion contra smoke tests vive en otros niveles del pipeline. El filtro tenia otro proposito (excluir DLLs `*.Tests.dll` de proyectos de tests).

Bug refinado en dos planos:

1. **Plano funcional**: filtro mal escrito impide instrumentacion. Fix de 2 lineas, validado.
2. **Plano observable**: el camino E del Stage 4 colapsa 4 causas distintas en un mismo `skipped` con warning generico. Imposible distinguir `bug del filtro` de `build_failed` o `collect_failed` desde el log.

## Acciones

### Fix principal (sigue siendo el mismo)

Archivo: `scripts/tdd-pipeline.sh`, lineas 1102-1104.

```bash
# Antes
for dll in "$WORKTREE_PATH"/tests/Bitakora.ControlAsistencia.*.Tests/bin/Debug/net10.0/Bitakora.ControlAsistencia.*.dll; do
    [[ "$dll" == *Tests* ]] && continue
    [[ ! -f "$dll" ]] && continue

# Despues
for dll in "$WORKTREE_PATH"/tests/Bitakora.ControlAsistencia.*.Tests/bin/Debug/net10.0/Bitakora.ControlAsistencia.*.dll; do
    [[ ! -f "$dll" ]] && continue
    bn="$(basename "$dll")"
    [[ "$bn" == *Tests.dll ]] && continue
```

Sin riesgo para smoke tests (ya excluidos por el glob).

### Cambios complementarios (no criticos, no bloquean PR)

- Logging granular en `measure_coverage` (cuantas DLLs vio el glob, cuantas filtro, cuantas instrumento). Permite diagnosticar el bug en una sola lectura de log.
- En el evento de `skipped` por camino E (linea 1199), distinguir las 4 causas: `build_failed`, `no_dlls_instrumented`, `collect_failed`, `xml_missing`.
- Deduplicar instrumentacion de `Contracts.dll` (se replica en cada test project).

### Lo que NO debe hacerse

- **No** convertir el camino E en `failed`. Viola ADR-0018 lineas 83 y 87.
- **No** tocar el comportamiento del camino C (PRs sin `src/*.cs`) — cubre legitimamente PRs de smoke tests, docs, infra, tooling.
- **No** tocar el glob `*.Tests/` del bucle — ya excluye SmokeTests correctamente.

### Issue propuesto

- Titulo: `Corregir filtro de DLLs en Stage 4 que omite la instrumentacion completa`
- Labels: `bug,tipo:tooling,dom:tooling,estado:listo`
- Cuerpo: bug del filtro (fix principal de 2 lineas, validado), evidencia de 3 corridas confirmadas (issues #106, #108 y otra del 21-04), constatacion de que el gate nunca ha medido cobertura desde 2026-04-11. Mencionar como mejoras complementarias el logging granular y la diferenciacion de causas, sin hacerlas requisito del PR para no inflar el alcance. Mencionar que el fix respeta el caracter no-bloqueante del ADR-0018.

Pendiente de confirmacion del usuario para crear.

## Preguntas abiertas

- En el log del 2026-04-21 16:26 (issue #106) aparece `MarcacionAdicionada.cs` clasificado como "no evaluado", aunque vive en `Eventos/`. La clasificacion para Eventos requiere `static.*Crear(` (linea 1017). Es un bug aparte de clasificacion, o el archivo realmente no tiene factory `Crear`? Vale la pena verificarlo en otro turno.
- El comentario del commit `e7a7f2c` dice que el `dotnet-coverage collect "dotnet test --solution …"` (linea 1123) "tiene su propio fallback y no bloquea la creacion del PR". Habria que validar que efectivamente colectar cobertura sobre la solucion completa (incluyendo SmokeTests) no genera ruido en el coverage XML por requests fallidos a dev. Si lo genera, el `extract_file_coverage` tal vez lo ignora porque solo busca por basename de archivos de logica del PR.
- El parche propuesto deja la oportunidad de instrumentar la misma DLL multiple veces (Contracts.dll vive en los tres test projects). `dotnet-coverage instrument` reescribe en sitio; instrumentaciones repetidas pueden provocar warnings o fallar. Convendria validarlo apenas se aplique el fix, idealmente con una corrida real del pipeline.
