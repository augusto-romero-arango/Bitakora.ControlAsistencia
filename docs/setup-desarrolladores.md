# Setup para nuevos desarrolladores

El marketplace y el plugin `mefisto@augusto-romero-arango-harness` ya están declarados en `.claude/settings.json` (commiteado al repo), así que la instalación es prácticamente automática:

1. **Acceso al repo del harness**: asegúrate de poder leer `augusto-romero-arango/eda-evsourcing-azure-harness`. Si es privado, autentica con `gh auth login` con permisos de lectura.
2. **Abre Claude Code en el repo**: detectará el marketplace y el plugin habilitado. Si no lo instala solo, corre `/plugin` y confirma la instalación de `mefisto`.
3. **Recarga** con `/reload-plugins` para activar skills y agentes sin reiniciar la sesión.

Para verificar que quedó: corre `/mefisto:health-check` o invoca cualquier skill `/mefisto:*` desde el prompt. Para traer cambios publicados en el harness: `/plugin update mefisto`.
