"""Clasifica el diff completo del PR para el workflow CI (sin dependencias externas)."""

import json
import os
import sys
from urllib.parse import quote
from urllib.request import Request, urlopen


PAGE_SIZE = 100
MAX_FILES = 3000  # Limite documentado de la API REST de archivos de un PR.
STATUSES = {"added", "modified", "removed", "renamed", "copied", "changed", "unchanged"}


def api_json(url, token):
    request = Request(url, headers={
        "Accept": "application/vnd.github+json",
        "Authorization": f"Bearer {token}",
        "X-GitHub-Api-Version": "2022-11-28",
    })
    with urlopen(request, timeout=30) as response:
        return json.load(response)


def documentary(path):
    return path in ("README.md", "CLAUDE.md") or path.startswith("docs/")


def classify(fetch, repo, number, token):
    if not repo or not number.isdecimal() or int(number) < 1 or not token:
        raise ValueError("Faltan metadatos validos del PR o GITHUB_TOKEN")
    base = f"https://api.github.com/repos/{quote(repo, safe='/')}/pulls/{int(number)}"
    metadata = fetch(base, token)
    if not isinstance(metadata, dict) or type(metadata.get("changed_files")) is not int:
        raise ValueError("Respuesta de metadatos del PR invalida")
    expected = metadata["changed_files"]
    if expected < 1 or expected > MAX_FILES:
        raise ValueError(f"Total de archivos no verificable: {expected} (limite {MAX_FILES})")

    seen = set()
    all_docs = True
    page = 1
    while True:
        files = fetch(f"{base}/files?per_page={PAGE_SIZE}&page={page}", token)
        if not isinstance(files, list) or len(files) > PAGE_SIZE:
            raise ValueError(f"Pagina {page} de archivos invalida")
        if not files and len(seen) != expected:
            raise ValueError(f"Pagina {page} vacia antes de completar el diff ({len(seen)}/{expected})")
        for item in files:
            if not isinstance(item, dict):
                raise ValueError(f"Entrada invalida en pagina {page}")
            path, status = item.get("filename"), item.get("status")
            if not isinstance(path, str) or not path or status not in STATUSES:
                raise ValueError(f"Ruta o estado invalido en pagina {page}")
            if path in seen:
                raise ValueError(f"Ruta duplicada en pagina {page}: {path!r}")
            seen.add(path)
            paths = [path]
            if status == "renamed":
                previous = item.get("previous_filename")
                if not isinstance(previous, str) or not previous:
                    raise ValueError(f"Renombrado sin ruta anterior: {path!r}")
                paths.append(previous)
            if not all(map(documentary, paths)):
                all_docs = False
        if len(seen) > expected:
            raise ValueError(f"La API devolvio mas archivos que los declarados ({len(seen)}/{expected})")
        if len(files) < PAGE_SIZE:
            if len(seen) != expected:
                raise ValueError(f"Diff incompleto ({len(seen)}/{expected})")
            return all_docs
        page += 1


def main():
    try:
        only_docs = classify(api_json, os.environ.get("GITHUB_REPOSITORY", ""),
                             os.environ.get("PR_NUMBER", ""), os.environ.get("GITHUB_TOKEN", ""))
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
            output.write(f"only_docs={'true' if only_docs else 'false'}\n")
        print(f"Archivos del PR: {'solo documentacion' if only_docs else 'CI completo'}")
    except (ValueError, KeyError, OSError, TypeError, json.JSONDecodeError) as error:
        print(f"::error::No se pudo verificar el diff completo del PR: {error}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
