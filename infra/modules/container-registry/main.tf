variable "name" {
  description = "Nombre del Container Registry (5-50 chars, SOLO alfanumerico -- Microsoft.ContainerRegistry/registries, scope global, no admite guiones a diferencia de otros recursos de este modulo; verificado contra Microsoft Learn, 'Naming rules and restrictions for Azure resources')"
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

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

# Registro de imagenes del worker de proyecciones (MEF-ADR-0034 seccion 8). SKU Basic:
# el worker es la unica imagen que este registry sirve. Sin prevent_destroy: a diferencia
# de Postgres/Storage/Service Bus/Key Vault, un registry de imagenes es recreable sin
# perdida de datos con estado real -- la imagen se reconstruye y reempuja desde el
# pipeline de CI de imagen (fuera de alcance de este issue).
resource "azurerm_container_registry" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = var.tags
}

output "id" {
  value = azurerm_container_registry.this.id
}

output "name" {
  value = azurerm_container_registry.this.name
}

output "login_server" {
  description = "Hostname del registry (ej. acrasistdev123456.azurecr.io). Lo consume container-app (bloque registry.server) y el pipeline de CI que construya/empuje la imagen del worker (fuera de alcance de este issue)"
  value       = azurerm_container_registry.this.login_server
}
