variable "name" {
  description = "Nombre de la storage account (3-24 chars, solo minusculas y numeros)"
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

resource "azurerm_storage_account" "this" {
  name                     = var.name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  # Sin acceso anonimo a blobs/contenedores: esta cuenta es el host de una
  # Function App (WEBSITE_RUN_FROM_PACKAGE, deployment slots, colas y tablas del
  # runtime) y nada de eso debe ser publico. Se declara EXPLICITAMENTE en vez de
  # heredar el default del provider: en azurerm 4.x el default era `true` y en
  # 5.0 pasa a `false` ("The property `allow_nested_items_to_be_public` now
  # defaults to `false`" -- guia oficial 5.0-upgrade-guide, seccion
  # azurerm_storage_account). Dejarlo implicito ataria una propiedad de
  # seguridad al major del provider: un rollback a 4.x volveria a permitir el
  # acceso anonimo en silencio. Declarado, el valor no depende de la version.
  # El diff `true -> false` que el plan del PR mostrara sobre las cuentas ya
  # existentes es el mismo con o sin esta linea (issue #304, CA-7).
  allow_nested_items_to_be_public = false

  tags = var.tags

  lifecycle {
    prevent_destroy = true
  }
}

output "id" {
  value = azurerm_storage_account.this.id
}

output "name" {
  value = azurerm_storage_account.this.name
}

output "primary_connection_string" {
  value     = azurerm_storage_account.this.primary_connection_string
  sensitive = true
}

output "primary_access_key" {
  description = "Access key primaria de la storage account"
  value       = azurerm_storage_account.this.primary_access_key
  sensitive   = true
}
