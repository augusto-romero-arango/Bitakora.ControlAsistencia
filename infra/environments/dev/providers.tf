terraform {
  required_version = ">= 1.6"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
  }

  backend "azurerm" {
    # Los valores se proveen via backend.tfvars o variables de entorno
    # terraform init -backend-config=backend.tfvars
    resource_group_name  = "rg-controlasistencias-tfstate"
    storage_account_name = "stcatfstatedev"
    container_name       = "tfstate"
    key                  = "dev.terraform.tfstate"

    # Backend keyless por AAD (MEF-ADR-0025 decision #8): el plano de datos del
    # blob se autentica por Microsoft Entra ID/RBAC en vez de la account key.
    # ARM_USE_OIDC habilita tanto al provider azurerm como al backend azurerm a
    # autenticarse con el mismo token federado (MEF-ADR-0022).
    use_azuread_auth = true
  }
}

provider "azurerm" {
  subscription_id = var.subscription_id

  # Microsoft.App no esta en el set "core" que el provider registra por defecto
  # (resource_provider_registrations = "core"). Lo requiere el Container App
  # Environment del worker de proyecciones (MEF-ADR-0034 seccion 8). Ver issue #246.
  resource_providers_to_register = [
    "Microsoft.App",
  ]

  features {
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }
}
