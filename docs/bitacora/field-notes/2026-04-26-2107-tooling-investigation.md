---
fecha: 2026-04-26
hora: 21:07
sesion: tooling-investigator
tema: Stage 4 (Coverage Gate) del pipeline TDD nunca instrumenta DLLs por filtro `*Tests*` aplicado a la ruta absoluta
---

## Sintoma reportado

En la corrida del pipeline TDD del issue #108 (rama `worktree-issue-108-emitir-diacalculado-tras-adicionar-marca`), Stage 4 reporta:

```
[20:43:42] Compilando proyecto para instrumentacion...
[20:43:45] Instrumentando DLLs...
⚠ No se instrumento ninguna DLL
⚠ La instrumentacion/medicion de cobertura fallo — continuando sin coverage gate
```

El gate se omite silenciosamente y el PR sale sin tabla de cobertura, perdiendo la red de seguridad sobre los archivos de logica del cambio (`AdicionarMarcacionCuandoMarcacionRegistradaCommandHandler.cs` y `ControlDiarioAggregateRoot.cs`).

Log completo: `.claude/pipeline/logs/pipeline-20260426-201134.log` lineas 285-342.

## Investigacion

### Localizacion del Stage 4

El Stage 4 vive en `scripts/tdd-pipeline.sh` (lineas 947-1517). El bloque relevante:

- 1090-1136: funcion `measure_coverage()` — compila, instrumenta DLLs, recolecta cobertura.
- 1099-1110: bucle de instrumentacion (la pieza rota).
- 1112-1115: rama del fallo: `if [ "$instrumented" -eq 0 ]; then warn "No se instrumento ninguna DLL"; return 1; fi`.

Glob y filtros del bucle:

```bash
for dll in "$WORKTREE_PATH"/tests/Bitakora.ControlAsistencia.*.Tests/bin/Debug/net10.0/Bitakora.ControlAsistencia.*.dll; do
    [[ "$dll" == *Tests* ]] && continue
    [[ ! -f "$dll" ]] && continue
    if dotnet-coverage instrument "$dll" --settings "$settings_xml" >>"${LOG_FILE_ABS:-$LOG_FILE}" 2>&1; then
        instrumented=$((instrumented + 1))
    else
        warn "No se pudo instrumentar: $(basename "$dll")"
    fi
done
```

### Estado del entorno (todo OK)

- `dotnet-coverage` instalada como global tool en `/Users/augusto-romero-arango/.dotnet/tools/dotnet-coverage` version `18.6.2`.
- SDK activo: `.NET 10.0.201` (segun `global.json`).
- Glob expande correctamente y produce 8 DLLs (entre prod y test) en los tres test projects (`Contracts.Tests`, `ControlHoras.Tests`, `Programacion.Tests`).
- Ejemplo de DLL de produccion presente: `tests/Bitakora.ControlAsistencia.ControlHoras.Tests/bin/Debug/net10.0/Bitakora.ControlAsistencia.ControlHoras.dll`.
- `dotnet-coverage.settings.xml` correcto (incluye `.*Bitakora\.ControlAsistencia\..*\.dll$`, excluye `.*\.Tests\.dll$`).

### Reproduccion manual (bash, no zsh — mismo shebang del pipeline)

Ejecutando el bucle exacto del script con `bash -c`, el resultado fue:

```
Total instrumentadas=0 falladas=0
```

Cero invocaciones a `dotnet-coverage`. Inspeccionando cada iteracion:

```
DLL: .../tests/Bitakora.ControlAsistencia.Contracts.Tests/bin/Debug/net10.0/Bitakora.ControlAsistencia.Contracts.dll
  basename: Bitakora.ControlAsistencia.Contracts.dll
  skip_por_*Tests*: true     ← TODAS marcan true
```

Esto se confirma viendo el log real de la corrida: entre `Instrumentando DLLs...` y `No se instrumento ninguna DLL` NO aparece ningun mensaje `No se pudo instrumentar: …`. Si el `for` hubiera ejecutado `dotnet-coverage` y este hubiera fallado, deberian aparecer warnings por DLL.

## Diagnostico

**Causa raiz**: el filtro `[[ "$dll" == *Tests* ]] && continue` se aplica contra la **ruta absoluta** completa de cada DLL, no contra su basename. Como el glob entra al directorio `tests/Bitakora.ControlAsistencia.*.Tests/bin/Debug/net10.0/`, la subcadena `.Tests` aparece en TODAS las rutas. El patron `*Tests*` matchea siempre y `continue` salta cada iteracion sin instrumentar nada.

Ejemplo de DLL de produccion que deberia instrumentarse pero es saltada:

```
/Users/.../tests/Bitakora.ControlAsistencia.ControlHoras.Tests/bin/Debug/net10.0/Bitakora.ControlAsistencia.ControlHoras.dll
                                              ^^^^^                                ↑ esta DLL es de produccion, no es .Tests.dll
                                              esta substring activa el continue
```

**Validacion del fix**: cambiando el filtro a operar sobre el basename y exigiendo el sufijo `.dll`, las 5 DLLs de produccion se instrumentan correctamente:

```bash
bn="$(basename "$dll")"
[[ "$bn" == *Tests.dll ]] && continue
```

Output observado tras el cambio:

```
>>> Bitakora.ControlAsistencia.Contracts.dll
El archivo de entrada se instrumentó correctamente.
>>> Bitakora.ControlAsistencia.ControlHoras.dll
El archivo de entrada se instrumentó correctamente.
>>> Bitakora.ControlAsistencia.Programacion.dll
El archivo de entrada se instrumentó correctamente.
Total instrumentadas=5
```

(`Contracts.dll` aparece varias veces porque vive replicada en cada test project; el ProjectReference la copia. Eso ya esta cubierto por el `Exclude` del settings.xml del segundo paso `dotnet-coverage collect`, pero podria evitarse instrumentandola una sola vez en el futuro.)

**Cuando se introdujo**: commit `462cea8` (`feat(pipeline): agregar Stage 4 coverage gate al pipeline TDD`, 2026-04-11) fue quien creo el bloque con el filtro defectuoso. Lleva ~15 dias enmascarando el coverage gate en todas las corridas.

**Impacto**: silencioso. El gate retorna `skipped` sin error, los pipelines pasan, los PRs se crean sin tabla de cobertura ni remediacion. Issues afectados desde el 11-abr probablemente todos los que hayan corrido `/implement`.

## Acciones

Propuesta de fix (NO aplicada — comunicar via issue):

- Archivo: `scripts/tdd-pipeline.sh`
- Linea 1102-1103, dentro de `measure_coverage()`:

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

Issue propuesto (pendiente de confirmacion del usuario para crearlo):

- Titulo: `Corregir filtro de DLLs en Stage 4 que omite la instrumentacion completa`
- Labels: `bug,tipo:tooling,dom:tooling,estado:listo`
- Cuerpo: descripcion del bug, evidencia del log, propuesta de fix de una linea, recomendacion de validar tambien el bucle de la siguiente iteracion (post-remediacion) en el mismo script.

Verificacion adicional sugerida en el issue: revisar el resto del script por si el mismo patron `*Tests*` aplicado a ruta vs basename aparece en otros bucles.

## Preguntas abiertas

- Por que el watchdog `kill -9` con `>/dev/null 2>&1 &` no exhibe el problema en el log de eventos. Convendria que `measure_coverage` reporte explicitamente en `events.log` cuantas DLLs encontro el glob (y cuantas saltaron por filtro), para detectar este tipo de degradacion silenciosa en futuras corridas.
- Conviene definir un **gate G4 estricto** que falle el pipeline cuando `measure_coverage` no instrumenta ninguna DLL en lugar de degradar a `skipped`. Hoy el `skipped` enmascara fallos de configuracion.
- Las DLLs `Bitakora.ControlAsistencia.Contracts.dll` se instrumentan multiples veces (una por test project). `dotnet-coverage instrument` reescribe el archivo en sitio; instrumentaciones repetidas sobre la misma DLL podrian generar ruido. Vale la pena deduplicar por basename o instrumentar solo desde `src/*/bin/Debug/net10.0/`.
- No verifique todavia si el Stage 4 en otras ramas (que ya hayan mergeado) sufrio el mismo skip. Conviene revisar `tooling-history.jsonl` para cuantificar el impacto historico.
