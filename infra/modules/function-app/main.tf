variable "name" {
  description = "Nombre de la Function App"
  type        = string
}

variable "resource_group_name" {
  description = "Nombre del resource group"
  type        = string
}

variable "location" {
  description = "Region de Azure"
  type        = string
}

variable "service_plan_id" {
  description = "ID del service plan"
  type        = string
}

variable "storage_account_name" {
  description = "Nombre de la storage account"
  type        = string
}

variable "app_insights_connection_string" {
  description = "Connection string de Application Insights"
  type        = string
  sensitive   = true
}

variable "app_settings" {
  description = "Variables de entorno adicionales de la funcion"
  type        = map(string)
  default     = {}
}

variable "always_on" {
  description = <<-EOT
    Always On del site_config (issue #400). Recomendado true en App Service
    plans dedicados (Basic/Standard/Premium): en esos tiers la VM se factura
    24/7 este o no la app dormida, asi que apagarlo no ahorra costo (doc
    oficial, "Cost of App Service plans") y ademas interrumpe el poll en
    background del agente de durabilidad de Wolverine (DurabilityMode.Solo,
    MEF-ADR-0020) cuando el host se duerme por inactividad.
  EOT
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

resource "azurerm_linux_function_app" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location

  service_plan_id               = var.service_plan_id
  storage_account_name          = var.storage_account_name
  storage_uses_managed_identity = true

  # La version del runtime de Functions se fija aqui, no en app_settings: el
  # provider escribe este valor en la key FUNCTIONS_EXTENSION_VERSION del app
  # setting por su cuenta. Declararla tambien a mano en app_settings genera un
  # diff perpetuo entre las dos vias (issue #305). Se deja explicita en vez de
  # apoyarse en el default documentado ("~4") para que la version del runtime
  # quede visible en el HCL.
  functions_extension_version = "~4"

  site_config {
    always_on = var.always_on

    application_stack {
      dotnet_version              = "10.0"
      use_dotnet_isolated_runtime = true
    }

    # La connection string de Application Insights se declara aqui, no en
    # app_settings: la doc de azurerm_linux_function_app indica que para
    # configuracion de Application Insights se use
    # application_insights_connection_string/application_insights_key, y que
    # el provider escribe el valor en la key APPLICATIONINSIGHTS_CONNECTION_STRING
    # del app setting por su cuenta ("For application insight related settings,
    # please use application_insights_connection_string and
    # application_insights_key, terraform will assign the value to the key
    # APPINSIGHTS_INSTRUMENTATIONKEY and APPLICATIONINSIGHTS_CONNECTION_STRING
    # in app setting" -- registry.terraform.io, docs/resources/linux_function_app,
    # seccion app_settings). Declararla tambien a mano en app_settings genera
    # un diff perpetuo entre las dos vias (issue #305): site_config.application_insights_connection_string
    # es Computed y el provider la rellena leyendo el app setting existente en
    # Azure, por lo que un app_settings sin esta clave la nulifica en cada plan.
    application_insights_connection_string = var.app_insights_connection_string
  }

  # WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED NO se declara aqui: es una
  # optimizacion de cold start exclusiva del plan Consumption (doc oficial,
  # "App settings reference for Azure Functions", entrada
  # WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED), inerte en Linux dedicado (B1,
  # MEF-ADR-0020) porque ahi no existe la fase de worker placeholder que la
  # plataforma reutiliza al especializar el sitio; ademas la plataforma la fija
  # sola cuando aplica (Azure/azure-functions-host#10445, kshyju, 2024-09-04:
  # "This app setting entry is automatically set by the platform now. So, no
  # user action needed."). Se retiro en el issue #677 tras copiarse sin fuente
  # propia de otro repo (commit 382e67c).
  app_settings = merge(
    {
      FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
      WEBSITE_RUN_FROM_PACKAGE = "1"
    },
    var.app_settings
  )

  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

output "id" {
  value = azurerm_linux_function_app.this.id
}

output "name" {
  value = azurerm_linux_function_app.this.name
}

output "principal_id" {
  description = "Principal ID de la managed identity"
  value       = azurerm_linux_function_app.this.identity[0].principal_id
}

output "default_hostname" {
  description = "Hostname por defecto de la Function App (ej. func-x.azurewebsites.net). Valor computado por Azure: usarlo en vez de concatenar name + .azurewebsites.net protege contra hostnames regionalizados"
  value       = azurerm_linux_function_app.this.default_hostname
}
