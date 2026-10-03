## Resultado

Aprobado con una corrección menor. El diff del writer cumple los CA del issue #775 y es idéntico en los 6 workflows (comparado línea a línea):

- CA-1: `build-and-test` y `deploy` mantienen `debe_desplegar == 'true'` y agregan la condición de origen confiable: `event_name != 'workflow_run'`, o bien `workflow_run.event == 'push'`, `head_branch == 'main'` y `head_repository.full_name == github.repository`. La condición va dentro del `if:` de cada job.
- CA-2: `determinar-alcance` recibe `RUN_EVENTO` y `RUN_REPO` por `env:`. No hay interpolación `${{ }}` dentro del `run`.
- CA-3: cada job lleva un comentario breve con el porqué y una referencia al comentario de `determinar-alcance`.
- CA-4: con `push` y `workflow_dispatch` la condición es verdadera porque `event_name != 'workflow_run'`.
- CA-5: actionlint da limpio en los 6 archivos.
- Los jobs `smoke-tests` no se tocaron. `deploy-projections.yml` no usa `workflow_run` y no tiene alertas, así que quedó fuera, como corresponde.
- No hay cambios fuera del alcance ni rutas prohibidas.

## Correcciones

- El comentario de `determinar-alcance` seguía diciendo "se filtra igual por conclusion y rama". Lo alineé con la guarda nueva: ahora menciona el evento de origen (`push`), el repositorio de origen y que los jobs que hacen checkout repiten la condición en su `if:`. Aplicado en los 6 workflows; en `deploy-mcp-comandos.yml` el comentario tenía otra redacción y se ajustó en su lugar. Commit `Alinear el comentario de determinar-alcance con la guarda de origen confiable (#775)`.

## Verificacion

- `actionlint` sobre `deploy-colaboradores`, `deploy-control-horas`, `deploy-mcp-consultas`, `deploy-programacion` y `deploy-sedes`: sin errores.
- `deploy-mcp-comandos.yml`: un solo error, `unexpected key "queue" for "concurrency"` en el job `smoke-tests`. Ya existía antes de este issue (está en el commit base): la versión instalada de actionlint no reconoce `concurrency.queue`. Este diff no lo introduce.
- No hay código C#: no aplica build ni tests.
- Queda pendiente (post-merge, fuera del pipeline): ver si CodeQL cierra las 12 alertas `actions/untrusted-checkout/high`.
