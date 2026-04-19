# La ventana es ahora

Presentación para los líderes de equipo y el dueño de SincoERP sobre el impacto de la IA en el oficio del desarrollo.

Construida con [Slidev](https://sli.dev) — markdown puro, diffs claros, estética sobria tipo NDC/Goto.

## Cómo usarla

```bash
npm install       # una sola vez
npm run dev       # abre la presentación en el navegador con hot-reload
```

Pasás diapositivas con flechas. Editás `slides.md` y los cambios se reflejan en caliente.

## Exportar a PDF (respaldo offline)

```bash
npm run export-pdf
```

Genera un PDF con todas las diapositivas. Útil si el WiFi del evento falla.

## Cómo iterar

Dos caminos, ambos válidos:

1. **Directo**: abrís `slides.md` en tu editor y cambiás lo que sea. Una idea por slide, separadas por `---`.
2. **Vía chat**: le decís al asistente qué slide cambiar y cómo. Él aplica el diff.

## Estructura

- `slides.md` — el deck completo.
- `public/` — imágenes (diagramas descargados, fotos si Augusto las provee).
- `components/` — componentes Vue custom si los llegamos a necesitar.
- `package.json` — dependencias Slidev.

## Arco narrativo

Cuatro actos + prólogo + cierre. ~40 diapositivas, ~50 min hablados, ~10 min Q&A.

1. **Prólogo**: hook personal + tesis.
2. **Acto I** — El mundo se partió en dos: voces de la promesa vs voces críticas. Cierre: *la IA amplifica lo bueno y lo malo*.
3. **Acto II** — Vibe coding como oficio: FAAFO, 3 loops, Head Chef, caso Adidas, skills, prácticas de ingeniería como antídoto.
4. **Acto III** — Atributos arquitectónicos que favorecen agentes: serverless+IaC, EDA, ES, DORA como espejo, ControlAsistencia como evidencia.
5. **Acto IV** — El mandato: la IA como el nuevo junior que todos contratamos.
6. **Cierre** — Referencias y Q&A.

## Fuentes verificables

Todas las citas del deck tienen fuente (blog post, entrevista, paper, libro con página). El original en inglés vive en las notas del ponente; el texto principal está en español.

Los placeholders `[PENDIENTE]` marcan contenido que requiere confirmación explícita (foto del libro, cita exacta que no encontré online, anécdota personal del hook).
