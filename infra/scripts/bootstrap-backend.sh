#!/usr/bin/env bash
# Bootstrap del backend de Terraform en Azure.
# Crea el resource group, storage account y blob container para el state.
# Idempotente: si los recursos ya existen, no falla.
#
# Uso:
#   ./infra/scripts/bootstrap-backend.sh
#   ./infra/scripts/bootstrap-backend.sh --env dev --location eastus2 --project controlasistencias
#
# Opciones:
#   --env       Ambiente (default: dev)
#   --location  Region de Azure (default: eastus2)
#   --project   Nombre corto del proyecto (default: controlasistencias)

set -euo pipefail

# ----- Valores por defecto -----
ENV="dev"
LOCATION="eastus2"
PROJECT="controlasistencias"

# ----- Parsear argumentos -----
while [[ $# -gt 0 ]]; do
  case "$1" in
    --env)       ENV="$2";      shift 2 ;;
    --location)  LOCATION="$2"; shift 2 ;;
    --project)   PROJECT="$2";  shift 2 ;;
    *) echo "Argumento desconocido: $1" >&2; exit 1 ;;
  esac
done

# Nombre del storage account: sin guiones, max 24 chars
PROJECT_CLEAN="${PROJECT//-/}"
RG_NAME="rg-${PROJECT}-tfstate"
SA_NAME="st${PROJECT_CLEAN}tfstate${ENV}"
CONTAINER_NAME="tfstate"

# ----- Verificar prerequisitos -----
if ! command -v az &>/dev/null; then
  echo "ERROR: az cli no esta instalado. Instalar con: brew install azure-cli" >&2
  exit 1
fi

if ! az account show &>/dev/null; then
  echo "ERROR: No hay sesion activa de Azure. Ejecutar: az login" >&2
  exit 1
fi

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo "Suscripcion activa: ${SUBSCRIPTION_ID}"
echo ""
echo "Creando backend de Terraform para ambiente '${ENV}'..."
echo "  Resource group : ${RG_NAME}"
echo "  Storage account: ${SA_NAME}"
echo "  Container      : ${CONTAINER_NAME}"
echo "  Location       : ${LOCATION}"
echo ""

# ----- Crear resource group -----
if az group show --name "${RG_NAME}" &>/dev/null; then
  echo "[OK] Resource group '${RG_NAME}' ya existe"
else
  echo "Creando resource group '${RG_NAME}'..."
  az group create --name "${RG_NAME}" --location "${LOCATION}" --output none
  echo "[OK] Resource group creado"
fi

# ----- Crear storage account -----
if az storage account show --name "${SA_NAME}" --resource-group "${RG_NAME}" &>/dev/null; then
  echo "[OK] Storage account '${SA_NAME}' ya existe"
else
  echo "Creando storage account '${SA_NAME}'..."
  az storage account create \
    --name "${SA_NAME}" \
    --resource-group "${RG_NAME}" \
    --location "${LOCATION}" \
    --sku Standard_LRS \
    --kind StorageV2 \
    --output none
  echo "[OK] Storage account creada"
fi

# ----- Crear blob container -----
if az storage container show --name "${CONTAINER_NAME}" --account-name "${SA_NAME}" &>/dev/null 2>&1; then
  echo "[OK] Blob container '${CONTAINER_NAME}' ya existe"
else
  echo "Creando blob container '${CONTAINER_NAME}'..."
  az storage container create \
    --name "${CONTAINER_NAME}" \
    --account-name "${SA_NAME}" \
    --output none
  echo "[OK] Blob container creado"
fi

# ----- Verificacion final -----
echo ""
RESULT=$(az storage container show --name "${CONTAINER_NAME}" --account-name "${SA_NAME}" --query name -o tsv 2>/dev/null || echo "")
if [[ "${RESULT}" == "${CONTAINER_NAME}" ]]; then
  echo "Bootstrap completado. El backend esta listo para 'terraform init'."
  echo ""
  echo "Configuracion del backend:"
  echo "  resource_group_name  = \"${RG_NAME}\""
  echo "  storage_account_name = \"${SA_NAME}\""
  echo "  container_name       = \"${CONTAINER_NAME}\""
else
  echo "ERROR: No se pudo verificar el container '${CONTAINER_NAME}'" >&2
  exit 1
fi
