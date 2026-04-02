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

variable "storage_account_connection_string" {
  description = "Connection string de la storage account (para App Settings)"
  type        = string
  sensitive   = true
}

variable "storage_account_access_key" {
  description = "Access key de la storage account (requerida por azurerm_linux_function_app)"
  type        = string
  sensitive   = true
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

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

resource "azurerm_linux_function_app" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location

  service_plan_id            = var.service_plan_id
  storage_account_name       = var.storage_account_name
  storage_account_access_key = var.storage_account_access_key

  site_config {
    application_stack {
      dotnet_version              = "10.0"
      use_dotnet_isolated_runtime = true
    }
  }

  app_settings = merge(
    {
      APPLICATIONINSIGHTS_CONNECTION_STRING = var.app_insights_connection_string
      FUNCTIONS_EXTENSION_VERSION           = "~4"
      FUNCTIONS_WORKER_RUNTIME              = "dotnet-isolated"
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
