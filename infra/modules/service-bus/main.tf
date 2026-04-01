variable "name" {
  description = "Nombre del Service Bus namespace"
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

variable "sku" {
  description = "SKU del namespace: Basic, Standard, Premium"
  type        = string
  default     = "Standard"
}

variable "topics" {
  description = "Lista de topics a crear"
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

resource "azurerm_servicebus_namespace" "this" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = var.sku
  tags                = var.tags

  lifecycle {
    prevent_destroy = true
  }
}

resource "azurerm_servicebus_topic" "topics" {
  for_each     = toset(var.topics)
  name         = each.value
  namespace_id = azurerm_servicebus_namespace.this.id
}

output "id" {
  value = azurerm_servicebus_namespace.this.id
}

output "name" {
  value = azurerm_servicebus_namespace.this.name
}

output "default_primary_connection_string" {
  value     = azurerm_servicebus_namespace.this.default_primary_connection_string
  sensitive = true
}
