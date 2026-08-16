terraform {
  required_version = ">= 1.6"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 5.0"
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

  # En v5 el default de resource_provider_registrations es "none": el provider
  # ya no registra ningun Resource Provider automaticamente (antes de v5 el
  # default era "legacy", un set fijo de ~60 RPs). Por eso declarar
  # explicitamente los RPs que la configuracion necesita -- como ya haciamos
  # desde el issue #246 -- deja de ser un ajuste puntual para Microsoft.App y
  # pasa a ser la forma canonica de registrar Resource Providers en v5.
  #
  # Lista canonica de los trece namespaces del marco (MEF-ADR-0021, issue #439).
  # Antes solo se declaraba Microsoft.App: los otros doce venian por el
  # auto-registro implicito del modo "legacy" de v4, que el pin "~> 5.0" de este
  # entorno ya no provee (default "none"). Sin declararlos, el primer apply que
  # cree un recurso de un namespace no registrado falla con
  # 409 MissingSubscriptionRegistration.
  #
  # OJO con el casing: es case-sensitive (internal/resourceproviders/required.go
  # del provider). "microsoft.insights" va en minusculas -- la validacion del
  # argumento es best-effort y un typo degrada a un [WARN] en el plan, para
  # reaparecer como 409 en el apply.
  resource_providers_to_register = [
    "Microsoft.App",
    "Microsoft.Resources",
    "Microsoft.Storage",
    "Microsoft.ManagedIdentity",
    "Microsoft.Authorization",
    "Microsoft.Web",
    "Microsoft.KeyVault",
    "Microsoft.ServiceBus",
    "Microsoft.DBforPostgreSQL",
    "Microsoft.OperationalInsights",
    "microsoft.insights",
    "Microsoft.ContainerRegistry",
    "Microsoft.ApiManagement",
  ]

  features {
    resource_group {
      prevent_deletion_if_contains_resources = true
    }

    # v5 mueve enhanced_validation dentro de "features" y lo deshabilita por
    # default (antes iba fuera de "features" y estaba habilitado). Sin esto,
    # una location o Resource Provider invalido se detecta en el apply, no en
    # el plan. En este proyecto el plan corre en el PR y el apply corre al
    # mergear (MEF-ADR-0022): aceptar el default nuevo moveria esa clase de
    # error de "antes del merge" a "despues del merge". Se restaura
    # explicitamente el comportamiento de validacion en tiempo de plan que ya
    # teniamos en v4.
    enhanced_validation {
      locations          = true
      resource_providers = true
    }
  }
}
