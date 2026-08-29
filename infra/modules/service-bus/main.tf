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
      name                = string
      correlation_filter  = optional(map(string))
      default_message_ttl = optional(string)
      # Issue #463 (MEF-ADR-0026): fan-in dentro de un topic -- serializa la convergencia de
      # varias resoluciones sobre el mismo aggregate. ForceNew en Azure (no se puede alternar
      # sobre una subscription existente): default false preserva el comportamiento previo.
      requires_session = optional(bool, false)
    })), [])
  }))
  default = {}

  # ADR-0027: el enrutamiento multi-destinatario usa siempre un correlation
  # filter de igualdad (ADR-0001 rechaza SqlFilter sin excepcion). Cuando una
  # subscription declara correlation_filter debe traer al menos una property de
  # igualdad; un mapa vacio no describe ningun criterio y Azure lo rechazaria.
  validation {
    condition = alltrue(flatten([
      for topic in values(var.topics_config) : [
        for sub in topic.subscriptions :
        sub.correlation_filter == null ? true : length(sub.correlation_filter) > 0
      ]
    ]))
    error_message = "Cada correlation_filter debe declarar al menos una property de igualdad (ADR-0027)."
  }
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
        key                 = "${topic_name}/${sub.name}"
        topic_name          = topic_name
        sub_name            = sub.name
        correlation_filter  = sub.correlation_filter
        default_message_ttl = sub.default_message_ttl
        requires_session    = sub.requires_session
      }
    ]
  ])
  subscriptions_map = { for s in local.subscriptions_flat : s.key => s }
}

resource "azurerm_servicebus_subscription" "subs" {
  for_each            = local.subscriptions_map
  name                = each.value.sub_name
  topic_id            = azurerm_servicebus_topic.topics[each.value.topic_name].id
  max_delivery_count  = 10
  default_message_ttl = each.value.default_message_ttl
  requires_session    = each.value.requires_session
}

# ADR-0001 / ADR-0027: se removio el escape-hatch SqlFilter. El enrutamiento
# multi-destinatario se resuelve solo con correlation filter de igualdad sobre
# properties de la aplicacion (>= 1, garantizado por la validation de
# topics_config).
resource "azurerm_servicebus_subscription_rule" "filters" {
  for_each = {
    for k, v in local.subscriptions_map : k => v
    if v.correlation_filter != null
  }
  name            = "filter"
  subscription_id = azurerm_servicebus_subscription.subs[each.key].id
  filter_type     = "CorrelationFilter"

  correlation_filter {
    properties = each.value.correlation_filter
  }
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
