"""Pruebas ligeras de la politica del workflow CI, sin .NET ni Azure."""

import importlib.util
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("ci_pr_impact", ROOT / "scripts/ci_pr_impact.py")
impact = importlib.util.module_from_spec(spec)
spec.loader.exec_module(impact)


def file(path, status="modified", previous=None):
    item = {"filename": path, "status": status}
    if previous is not None:
        item["previous_filename"] = previous
    return item


def classify(files, total=None, fault=None):
    """Simula las respuestas de la API REST, incluida su paginacion real."""
    def fetch(url, _token):
        if "/files?" not in url:
            return {"changed_files": len(files) if total is None else total}
        page = int(url.split("page=")[-1])
        if page == fault:
            raise OSError("API no disponible")
        return files[(page - 1) * 100:page * 100]
    return impact.classify(fetch, "owner/repo", "42", "token")


class PolicyTests(unittest.TestCase):
    def test_documentos_y_recursos_sin_extension(self):
        self.assertTrue(classify([file("docs/bitacora/nota.md", "added"),
                                  file("docs/ddd/glosario.yaml"), file("docs/imagen.png"),
                                  file("docs/reporte.pdf"), file("docs/presentacion.html"),
                                  file("docs/recurso"), file("README.md"), file("CLAUDE.md")]))

    def test_borrado_y_renombrado_documental(self):
        self.assertTrue(classify([file("docs/viejo.md", "removed"),
                                  file("docs/nuevo.md", "renamed", "docs/anterior.md")]))

    def test_codigo_en_commit_anterior_y_ultimo_documental(self):
        # La API enumera el PR completo, no el ultimo commit.
        self.assertFalse(classify([file("src/codigo.cs"), file("docs/ultimo.md")]))

    def test_rutas_no_exentas(self):
        for path in (".gitignore", ".github/workflows/ci.yml", "global.json",
                     "ControlAsistencias.slnx", "src/nota.md", "tests/nota.md", "infra/nota.md"):
            with self.subTest(path=path):
                self.assertFalse(classify([file(path), file("README.md")]))

    def test_renombrados_en_ambos_sentidos(self):
        self.assertFalse(classify([file("docs/codigo.cs", "renamed", "src/codigo.cs")]))
        self.assertFalse(classify([file("src/codigo.cs", "renamed", "docs/codigo.cs")]))
        self.assertFalse(classify([file("docs/copia.cs", "copied", "src/codigo.cs")]))
        self.assertTrue(classify([file("docs/copia.md", "copied", "docs/original.md")]))
        # Si la API agrega una ruta anterior a otro estado, tampoco se ignora.
        self.assertFalse(classify([file("docs/codigo.cs", "modified", "src/codigo.cs")]))

    def test_archivo_no_documental_en_pagina_posterior(self):
        self.assertFalse(classify([file(f"docs/{n}.md") for n in range(100)] +
                                  [file("src/codigo.cs")]))

    def test_fallos_de_fuente_e_incompletitud(self):
        files = [file(f"docs/{n}.md") for n in range(100)]
        for kwargs in ({"fault": 2}, {"total": 101}, {"total": 99}):
            with self.subTest(kwargs=kwargs), self.assertRaises((ValueError, OSError)):
                classify(files, **kwargs)
        with self.assertRaises(ValueError):
            classify([])
        with self.assertRaises(ValueError):
            classify([file("docs/a.md")], total=3001)
        with self.assertRaises(ValueError):
            classify([file("docs/a.md", "renamed")])
        with self.assertRaises(ValueError):
            classify([file("docs/a.md"), file("docs/a.md")])
        # Una pagina completa exige consultar la siguiente, incluso si el total coincide.
        self.assertTrue(classify(files))

    def test_respuestas_invalidas(self):
        for response in (None, {}, {"changed_files": "1"}, {"changed_files": True}):
            with self.subTest(response=response), self.assertRaises(ValueError):
                impact.classify(lambda url, token: response, "owner/repo", "42", "token")
        with self.assertRaises(ValueError):
            impact.classify(lambda url, token: {"changed_files": 1} if "/files?" not in url else {},
                            "owner/repo", "42", "token")
        for status in ("unchanged", "desconocido", []):
            with self.subTest(status=status), self.assertRaises(ValueError):
                classify([file("docs/a.md", status)])
        with self.assertRaises(ValueError):
            classify([file("docs/a.md", "copied")])

    def test_wiring_workflow(self):
        text = (ROOT / ".github/workflows/ci.yml").read_text(encoding="utf-8")
        self.assertIn("pull_request:\n    branches: [main]", text)
        self.assertNotIn("paths-ignore:", text)
        self.assertNotIn("pull_request_target:", text)
        self.assertIn("run: python3 scripts/ci_pr_impact.py", text)
        for name in ("Setup .NET 10", "Restore", "Build", "Instalar dotnet-coverage",
                     "Instrumentar assemblies (cobertura estatica)", "Test con cobertura",
                     "Generar reporte de cobertura", "Publicar resumen en Job Summary",
                     "Comentar cobertura en el PR"):
            with self.subTest(step=name):
                section = text.split(f"      - name: {name}\n", 1)[1].split("      - name:", 1)[0]
                self.assertIn("steps.impact.outputs.only_docs == 'false'", section)
        self.assertIn("CI pesado omitido:", text)


if __name__ == "__main__":
    unittest.main()
