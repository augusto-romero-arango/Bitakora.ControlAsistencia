---
fecha: 2026-06-24
hora: 12:48
sesion: tooling-investigator
tema: Falso positivo "el refactoring perdio tests" en el pipeline TDD del issue #185
---

## Sintoma reportado

El pipeline TDD del issue #185 (refactor PURO: "Mover el modelo de calculo rico a
ControlHoras y renombrar DetalleRetardo a Retardo") aborto en Stage 3 (fase refactor)
con el error:

```
[12:37:58] Tests post-refactoring: 87 (baseline: 214)
✗ ERROR: El refactoring perdió tests: antes=214, después=87
```

El usuario pregunta cuales tests se perdieron y si la perdida esta justificada por el
refactor o fue un error del refactorizador (reviewer).

## Investigacion

### Estado del pipeline
- Sesion tmux `tdd-pipeline-185` (creada 12:08:44): terminada con abort. NO genero PR.
- Issue #185: sigue OPEN (labels tipo:refactor, dom:contracts, dom:control-horas, estado:listo).
- Worktree `worktree-issue-185-mover-el-modelo-de-calculo-rico-a-contro` (commit 927ccf2):
  intacto, 1 commit de refactor sobre la base origin/main (0dd0e6a, que ya incluye #183 y #184).

### Mecanismo del gate (codigo del plugin, NO modificable desde el consumidor)
Archivo: `~/.claude/plugins/cache/augusto-romero-arango-harness/mefisto/0.6.0/scripts/tdd-pipeline.sh`

- `run_tests_projects()` (lineas 149-171) itera el glob `tests/<prefix>.*.Tests/` en orden
  ALFABETICO y concatena el stdout de `dotnet test --project` de cada proyecto.
- `extract_test_count()` (lineas 132-137) hace `grep ... | head -1`: captura SOLO el conteo
  del PRIMER proyecto del output combinado.
- Gate de refactoring (lineas 656-668 baseline, 886-896 post): compara
  `extract_test_count(baseline)` vs `extract_test_count(post)`. Como ambos usan `head -1`,
  comparan unicamente el primer proyecto alfabetico = **Contracts.Tests**.

### Conteo real por proyecto (verificado con `dotnet test --project`)

| Proyecto            | Base 0dd0e6a | Refactor 927ccf2 | Delta |
|---------------------|--------------|------------------|-------|
| Contracts.Tests     | 214          | 87               | -127  |
| ControlHoras.Tests  | 169          | 296              | +127  |
| Programacion.Tests  | 42           | 42               | 0     |
| **Total**           | **425**      | **425**          | **0** |

El "baseline: 214" y el "después=87" del gate son exactamente el conteo de Contracts.Tests
antes y despues. El total global se conserva: 425 = 425. Los 127 tests que salen de Contracts
entran exactos en ControlHoras (214-87 = 127 = 296-169).

### Verificacion a nivel de nombre de test
- `git diff --name-status -M 0dd0e6a..927ccf2 -- tests/`: TODOS los cambios cross-proyecto son
  renames (R) de `Contracts.Tests/ValueObjects/ControlHoras/*` a `ControlHoras.Tests/ValueObjects/*`.
  Ni un solo `D` (delete) huerfano. Unico `A` (add): `IgualdadTestBase.cs` (helper nuevo, suma).
- `dotnet test --list-tests` en ambos lados -> 422 nombres totalmente calificados por lado.
- Tras normalizar el unico cambio semantico del refactor (namespace
  `Contracts.Tests.ValueObjects.ControlHoras.*` -> `ControlHoras.Tests.ValueObjects.*` y rename
  de tipo `DetalleRetardo` -> `Retardo`), los conjuntos quedan IDENTICOS: cero tests solo en base,
  cero tests solo en refactor.

## Diagnostico

Causa raiz: **falso positivo del gate de refactoring**, no perdida de cobertura.

El bug vive en `extract_test_count` (`head -1`) combinado con `run_tests_projects` (output
multi-proyecto). El gate compara solo el primer proyecto alfabetico (Contracts.Tests). Cualquier
refactor que MUEVA tests fuera del primer proyecto alfabetico dispara el falso "perdió tests",
aunque el total global se conserve o crezca. Es un bug generico del harness, reproducible en
cualquier consumidor con mas de un proyecto de tests.

Clasificacion de los tests "que ya no estan" en Contracts: el 100% cae en categoria (a) se
movieron/renombraron legitimamente y siguen corriendo en ControlHoras.Tests. Ninguno en (b)
innecesario ni (c) roto/eliminado por error. El reviewer (fase refactor) hizo bien su trabajo:
el refactor esta verde y completo.

## Acciones

- Draft cross-repo en el repo de Mefisto: PENDIENTE de confirmacion del usuario.
  Repo destino: augusto-romero-arango/eda-evsourcing-azure-harness
  Titulo propuesto: "Corregir extract_test_count para sumar tests de todos los proyectos en
  el gate de refactoring". Labels: estado:borrador, tipo:tooling.
  (URL se agregara aqui tras crearlo.)
- Workaround para #185: PENDIENTE de confirmacion. El refactor esta verde y recuperable;
  se puede crear el PR manualmente desde el worktree (push de la rama + gh pr create con
  Closes #185) o relanzar el pipeline desde la fase de PR. No ejecutado.

## Preguntas abiertas

- El gate compara solo el primer proyecto alfabetico tambien para el baseline; por simetria
  el falso positivo solo aparece cuando el PRIMER proyecto pierde tests. Conviene que la
  correccion en Mefisto sume TODOS los proyectos (no solo evite el head -1 del primero),
  para tambien detectar perdidas reales que hoy quedan ocultas si ocurren en proyectos no-primeros.
- Confirmar con el usuario si prefiere recuperar el PR de #185 manualmente o relanzar el pipeline.
