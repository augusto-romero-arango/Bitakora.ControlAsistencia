# Codificación agéntica: ¿el "Silver Bullet"?

Presentación para líderes de equipo sobre el impacto de la IA en el oficio del desarrollo (41 diapositivas, cuatro actos + prólogo y cierre, con notas del ponente).

**Ubicación provisional.** El deck vive en Claude Slides; esta carpeta es solo una copia de respaldo y no pertenece al dominio de ControlAsistencia. Se moverá fuera del repo.

- **Deck (fuente de verdad):** https://claude.ai/artifact/EnxAcPxuBKcgfW2DP3gjXX — privado; compártelo desde el menú *Share* del deck. Desde allí se presenta y se exporta a PDF o PPTX.
- **Estética:** tomada del deck *Cosmos* (Manrope + IBM Plex Sans, paleta azul Sinco, diapositivas "noche").

## Contenido de la carpeta

- `project/deck.json` — índice del deck: orden de diapositivas, secciones (actos) y tipografías.
- `project/slides/<id>.html` — una diapositiva por archivo, en el formato de Claude Slides; las notas del ponente van en el `<aside>` final de cada una.
- `cosmos-assets/` — fondos, isotipo y logotipos extraídos del deck Cosmos.

Las imágenes (fotos de los ponentes citados, portada del libro y recursos de Cosmos) están cargadas como assets del deck; los `src="/_blob/<id>"` de las diapositivas apuntan a ellas.

## Cómo editar

Edita directamente en el deck (editor visual o comentarios) o pídele a Claude cambios sobre él. Esta copia no se sincroniza sola: si cambias el deck y quieres conservar el respaldo, vuelve a descargar los archivos de `project/`.

Reemplaza a la versión anterior en Slidev (`docs/presentaciones/2026-04-vibe-coding-urgencia/`), eliminada en este mismo cambio.
