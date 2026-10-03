## Implementado
- En los 6 workflows de deploy (colaboradores, control-horas, programacion, sedes, mcp-comandos, mcp-consultas):
  - `build-and-test` y `deploy`: `if:` ahora exige `debe_desplegar == 'true'` Y (`event_name != 'workflow_run'` O (`workflow_run.event == 'push'` Y `head_branch == 'main'` Y `head_repository.full_name == github.repository`)). Se repite en cada job (no solo salida de `determinar-alcance`) para que CodeQL vea la guarda junto al checkout.
  - Comentario breve junto a la condicion (latente, por que va en el job con checkout).
  - `determinar-alcance`: `env` suma `RUN_EVENTO` y `RUN_REPO`; la guarda suma `event == push` y `head_repository == $REPO`. Sin `${{ }}` dentro del `run`.
- push/workflow_dispatch: `event_name != 'workflow_run'` es verdadero, sin cambio. `smoke-tests` intacto.

## Verificacion
- actionlint: sin errores nuevos. Unico reporte: `queue: max` en `concurrency` de deploy-mcp-comandos.yml (preexistente, fuera de alcance; actionlint no conoce la clave).
- Los 6 YAML parsean.

## Pendiente/bloqueos
- Verificacion post-merge (alertas CodeQL #22-#33, run por workflow_run) queda fuera del pipeline, segun el issue.
