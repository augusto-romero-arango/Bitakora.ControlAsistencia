# Alerta de drenaje (backlog) de la cola del control diario (issues #819 y #1378).
# Vive en el ambiente porque el modulo monitoring no conoce el namespace de Service Bus.
# Metrica de plataforma ActiveMessages: sin costo de ingestion en Application Insights.
# EntityName solo toma colas y topics (no subscriptions); el backlog real se acumula en la
# cola con sesion actualizar-control-diario (MEF-ADR-0026), a donde reenvian las subscriptions.

variable "umbral_backlog_control_diario" {
  description = "Promedio de mensajes activos en actualizar-control-diario por encima del cual se alerta que ControlHoras no drena. Calibrado con datos de dev del 2026-10-31: el camino en lote (100 a 200 colaboradores x 1 mes) deja ActiveMessages en 0; 200 es cinco veces el drenaje esperado"
  type        = number
  default     = 200
}

variable "ventana_backlog_control_diario" {
  description = "Ventana (ISO 8601) durante la cual el promedio debe superar el umbral. Calibrado con datos de dev del 2026-10-31: PT5M sobre muestras de 1 min ignora picos sanos de menos de un minuto"
  type        = string
  default     = "PT5M"
}

resource "azurerm_monitor_metric_alert" "backlog_control_diario" {
  name                = "${local.prefix}-backlog-control-diario"
  resource_group_name = module.resource_group.name
  scopes              = [module.service_bus.id]
  description         = "La cola actualizar-control-diario promedia mas de ${var.umbral_backlog_control_diario} mensajes activos durante ${var.ventana_backlog_control_diario} - ControlHoras no esta drenando"
  severity            = 2
  enabled             = true
  frequency           = "PT1M"
  window_size         = var.ventana_backlog_control_diario

  criteria {
    metric_namespace = "Microsoft.ServiceBus/namespaces"
    metric_name      = "ActiveMessages"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = var.umbral_backlog_control_diario

    dimension {
      name     = "EntityName"
      operator = "Include"
      values   = ["actualizar-control-diario"]
    }
  }

  action {
    action_group_id = module.monitoring.action_group_id
  }

  tags = local.tags
}
