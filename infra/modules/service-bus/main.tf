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

variable "topics_config" {
  description = "Topics con sus subscriptions opcionales"
  type = map(object({
    subscriptions = optional(list(object({
      name   = string
      filter = optional(string)
    })), [])
  }))
  default = {}
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
  for_each     = var.topics_config
  name         = each.key
  namespace_id = azurerm_servicebus_namespace.this.id
}

locals {
  subscriptions_flat = flatten([
    for topic_name, topic in var.topics_config : [
      for sub in topic.subscriptions : {
        key        = "${topic_name}/${sub.name}"
        topic_name = topic_name
        sub_name   = sub.name
        filter     = sub.filter
      }
    ]
  ])
  subscriptions_map = { for s in local.subscriptions_flat : s.key => s }
}

resource "azurerm_servicebus_subscription" "subs" {
  for_each           = local.subscriptions_map
  name               = each.value.sub_name
  topic_id           = azurerm_servicebus_topic.topics[each.value.topic_name].id
  max_delivery_count = 10
}

resource "azurerm_servicebus_subscription_rule" "filters" {
  for_each = {
    for k, v in local.subscriptions_map : k => v
    if v.filter != null
  }
  name            = "filter"
  subscription_id = azurerm_servicebus_subscription.subs[each.key].id
  filter_type     = "SqlFilter"
  sql_filter      = each.value.filter
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

output "topic_ids" {
  description = "IDs de los topics creados"
  value       = { for k, v in azurerm_servicebus_topic.topics : k => v.id }
}
