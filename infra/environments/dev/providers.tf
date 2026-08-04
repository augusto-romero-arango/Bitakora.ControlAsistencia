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
    # Ubicacion del state, declarada en el HCL (no via backend.tfvars): mismo
    # resource group, cuenta, contenedor y key que antes de la migracion a AAD.
    # NO pasar una access_key ni un sas_token por -backend-config: seria una
    # credencial en texto plano y contradice MEF-ADR-0025 decision #1.
    resource_group_name  = "rg-controlasistencias-tfstate"
    storage_account_name = "stcatfstatedev"
    container_name       = "tfstate"
    key                  = "dev.terraform.tfstate"

    # Backend keyless por AAD (MEF-ADR-0025 decision #8): el plano de datos del
    # blob se autentica por Microsoft Entra ID/RBAC en vez de la account key que
    # el backend resolvia via listKeys en cada corrida. La identidad de CI
    # necesita "Storage Blob Data Contributor" sobre la cuenta del tfstate.
    # Hoy el token AAD lo emiten las credenciales ARM_* del workflow; cuando el
    # issue #297 introduzca ARM_USE_OIDC, el mismo token federado servira al
    # provider azurerm y a este backend (MEF-ADR-0022).
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
