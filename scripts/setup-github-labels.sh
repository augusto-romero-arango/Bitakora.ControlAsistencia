#!/usr/bin/env bash
# Provisiona el esquema de labels del proyecto en GitHub.
# Elimina los 9 labels default y crea el esquema dimensional.
#
# Uso: ./scripts/setup-github-labels.sh
# Prerequisito: gh auth login

set -e

echo "Eliminando labels default de GitHub..."
for label in "bug" "documentation" "duplicate" "enhancement" "good first issue" "help wanted" "invalid" "question" "wontfix"; do
  gh label delete "$label" --yes 2>/dev/null && echo "  - eliminado: $label" || echo "  - no encontrado (ok): $label"
done

echo ""
echo "Creando labels de tipo (azul)..."
gh label create "tipo:feature"   --color "0052CC" --description "Funcionalidad nueva de dominio"
gh label create "tipo:infra"     --color "0052CC" --description "Infraestructura Azure / Terraform"
gh label create "tipo:refactor"  --color "0052CC" --description "Reestructuracion sin comportamiento nuevo"
gh label create "tipo:bug"       --color "0052CC" --description "Correccion de defecto"
gh label create "tipo:tooling"   --color "0052CC" --description "Mejoras a pipeline, agentes o scripts"

echo ""
echo "Creando labels de dominio (verde)..."
gh label create "dom:programacion" --color "0E8A16" --description "Dominio Programacion (turnos, horarios)"
gh label create "dom:contracts"    --color "0E8A16" --description "Contratos compartidos (eventos, value objects)"
gh label create "dom:asistencia"   --color "0E8A16" --description "Dominio Asistencia (marcaciones)"

echo ""
echo "Creando labels de estado (amarillo/rojo)..."
gh label create "estado:borrador" --color "FBCA04" --description "Idea capturada - requiere refinamiento antes del pipeline"
gh label create "estado:listo"    --color "B60205" --description "Refinado y listo para pipeline TDD o IaC"

echo ""
echo "Creando labels especiales..."
gh label create "bloqueado" --color "D93F0B" --description "Depende de otro issue aun no cerrado"

echo ""
echo "Listo. Labels actuales:"
gh label list
