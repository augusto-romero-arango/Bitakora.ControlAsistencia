variable "name" {
  description = "Prefijo de nombre para los recursos de monitoreo"
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

variable "daily_data_cap_in_gb" {
  description = "Techo diario de ingestion en GB para Application Insights (0.5 GB ~ $35/mes maximo)"
  type        = number
  default     = 0.5
}

variable "alert_action_group_email" {
  description = "Email para recibir alertas de costos y picos de excepciones"
  type        = string
  default     = "augromara@gmail.com"
}

variable "daily_cap_warning_percent" {
  description = "Porcentaje del daily cap en el que se dispara la alerta de advertencia"
  type        = number
  default     = 80
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

resource "azurerm_log_analytics_workspace" "this" {
  name                = "${var.name}-logs"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_application_insights" "this" {
  name                                  = "${var.name}-ai"
  location                              = var.location
  resource_group_name                   = var.resource_group_name
  workspace_id                          = azurerm_log_analytics_workspace.this.id
  application_type                      = "web"
  daily_data_cap_in_gb                  = var.daily_data_cap_in_gb
  daily_data_cap_notifications_disabled = false
  tags                                  = var.tags
}

resource "azurerm_monitor_action_group" "cost_alerts" {
  name                = "${var.name}-cost-alerts"
  resource_group_name = var.resource_group_name
  short_name          = "CostAlert"

  email_receiver {
    name          = "admin"
    email_address = var.alert_action_group_email
  }

  tags = var.tags
}

# Alerta 1: ingestion diaria supera el 80% del daily cap (evaluada cada hora)
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "ingestion_warning" {
  name                = "${var.name}-ingestion-warning"
  resource_group_name = var.resource_group_name
  location            = var.location
  description         = "La ingestion diaria de Application Insights supera el ${var.daily_cap_warning_percent}% del daily cap - posible runaway"
  severity            = 2
  enabled             = true

  scopes               = [azurerm_log_analytics_workspace.this.id]
  evaluation_frequency = "PT1H"
  window_duration      = "P1D"

  criteria {
    query = <<-QUERY
      let dailyCapGB = ${var.daily_data_cap_in_gb};
      let warningThresholdGB = dailyCapGB * ${var.daily_cap_warning_percent} / 100;
      Usage
      | where TimeGenerated > ago(1d)
      | summarize TotalGB = sum(Quantity) / 1024
      | where TotalGB > warningThresholdGB
    QUERY

    time_aggregation_method = "Count"
    operator                = "GreaterThan"
    threshold               = 0

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.cost_alerts.id]
  }

  tags = var.tags
}

# Alerta 2: pico de excepciones >50 en 5 minutos (patron de funcion en loop de errores)
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "exception_spike" {
  name                = "${var.name}-exception-spike"
  resource_group_name = var.resource_group_name
  location            = var.location
  description         = "Pico de excepciones detectado - posible funcion en loop de errores generando costos"
  severity            = 1
  enabled             = true

  scopes               = [azurerm_application_insights.this.id]
  evaluation_frequency = "PT5M"
  window_duration      = "PT5M"

  criteria {
    query = <<-QUERY
      exceptions
      | where timestamp > ago(5m)
      | summarize ExceptionCount = count()
      | where ExceptionCount > 50
    QUERY

    time_aggregation_method = "Count"
    operator                = "GreaterThan"
    threshold               = 0

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.cost_alerts.id]
  }

  tags = var.tags
}

output "connection_string" {
  value     = azurerm_application_insights.this.connection_string
  sensitive = true
}

output "instrumentation_key" {
  value     = azurerm_application_insights.this.instrumentation_key
  sensitive = true
}
