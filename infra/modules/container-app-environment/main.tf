variable "name" {
  description = "Nombre del Container App Environment"
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

variable "log_analytics_workspace_id" {
  description = "ID del Log Analytics Workspace (output log_analytics_workspace_id del modulo monitoring) -- requerido junto con logs_destination = \"log-analytics\" (verificado contra la doc del provider azurerm, azurerm_container_app_environment). Reutiliza el mismo workspace que ya usa Application Insights (ADR-0009 local): evita crear un segundo workspace redundante."
  type        = string
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

# Entorno gestionado de Container Apps (MEF-ADR-0034 seccion 8). Se instancia una sola
# vez por entorno, igual que postgresql/service-bus/key-vault.
resource "azurerm_container_app_environment" "this" {
  name                       = var.name
  resource_group_name        = var.resource_group_name
  location                   = var.location
  logs_destination           = "log-analytics"
  log_analytics_workspace_id = var.log_analytics_workspace_id
  tags                       = var.tags
}

output "id" {
  value = azurerm_container_app_environment.this.id
}

output "name" {
  value = azurerm_container_app_environment.this.name
}

output "default_domain" {
  description = "Dominio publico por defecto del entorno (no aplica al worker: no lleva ingress, MEF-ADR-0034 seccion 8)"
  value       = azurerm_container_app_environment.this.default_domain
}
