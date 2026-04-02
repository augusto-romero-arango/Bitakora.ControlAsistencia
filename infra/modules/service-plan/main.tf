variable "name" {
  description = "Nombre del service plan"
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

variable "sku_name" {
  description = "SKU del plan: B1=Basic (minimo para .NET 10), EP1=Elastic Premium"
  type        = string
  default     = "B1"
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

resource "azurerm_service_plan" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = "Linux"
  sku_name            = var.sku_name
  tags                = var.tags
}

output "id" {
  value = azurerm_service_plan.this.id
}
