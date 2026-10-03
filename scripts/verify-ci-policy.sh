#!/usr/bin/env bash
set -euo pipefail

PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s scripts -p 'test_ci_pr_impact.py' -v
ruby -e 'require "yaml"; YAML.parse_file(".github/workflows/ci.yml"); puts "Sintaxis YAML valida"'
