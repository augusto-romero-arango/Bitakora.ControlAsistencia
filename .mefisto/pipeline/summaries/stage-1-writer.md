## Implementado
- `CI / build-and-test` clasifica todo el diff del PR mediante la API REST paginada, coteja `changed_files` y falla ante respuestas vacias, incompletas o invalidas. Evalua las dos rutas de renombrados. Solo omite pasos pesados para `docs/**`, `README.md` y `CLAUDE.md` raiz; emite un Job Summary explicito.
- El camino no documental conserva restore, build, tests y cobertura, incluidos los pasos con `always()`. No se modificaron los otros workflows ni los triggers/permisos de CI.
- `scripts/test_ci_pr_impact.py` prueba el clasificador real y el wiring; `scripts/verify-ci-policy.sh` ejecuta las pruebas y valida YAML.

## Verificacion
- `bash scripts/verify-ci-policy.sh`: 9 pruebas exitosas y sintaxis YAML valida con Ruby Psych. Incluye paginas posteriores, errores de API, diff incompleto, renombrados, borrados y rutas mixtas.
- `git diff --check`: sin errores.

## Pendiente/bloqueos
- No se ejecuto un PR real en GitHub Actions desde este stage; pendiente evidencia del run de CI tras publicar el PR. No se ejecuto .NET (no se cambio C#).
