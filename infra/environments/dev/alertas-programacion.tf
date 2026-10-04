# Alerta de drenaje (backlog) de la programacion masiva (issue #819).
# Vive en el ambiente porque el modulo monitoring no conoce el namespace de Service Bus.
# Metrica de plataforma ActiveMessages: sin costo de ingestion en Application Insights.

variable "umbral_backlog_programacion" {
  description = "Promedio de mensajes activos por encima del cual se alerta el backlog de programacion (suposicion sin datos; se ajusta con la primera programacion masiva real)"
  type        = number
  default     = 1000
}

variable "ventana_backlog_programacion" {
  description = "Ventana (ISO 8601) durante la cual el promedio debe superar el umbral"
  type        = string
  default     = "PT15M"
}

locals {
  # CA-5 del issue: el formato de EntityName para suscripciones debe confirmarse con
  # az monitor metrics list antes del apply. Si ActiveMessages solo existe a nivel topic,
  # cambiar estos valores por los dos topics (incluye la suscripcion smoke-tests, TTL 5 min).
  entidades_backlog_programacion = [
    "control-horas-escucha-programacion",
    "control-horas-escucha-dia-depurado",
  ]
}

resource "azurerm_monitor_metric_alert" "backlog_programacion" {
  name                = "${local.prefix}-backlog-programacion"
  resource_group_name = module.resource_group.name
  scopes              = [module.service_bus.id]
  description         = "Las suscripciones de ControlHoras a la programacion acumulan mensajes activos (promedio > ${var.umbral_backlog_programacion} durante ${var.ventana_backlog_programacion}) - la programacion tarda en verse"
  severity            = 2
  enabled             = true
  frequency           = "PT5M"
  window_size         = var.ventana_backlog_programacion

  criteria {
    metric_namespace = "Microsoft.ServiceBus/namespaces"
    metric_name      = "ActiveMessages"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = var.umbral_backlog_programacion

    dimension {
      name     = "EntityName"
      operator = "Include"
      values   = local.entidades_backlog_programacion
    }
  }

  action {
    action_group_id = module.monitoring.action_group_id
  }

  tags = local.tags
}
