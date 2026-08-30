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
  name                 = "${var.name}-ai"
  location             = var.location
  resource_group_name  = var.resource_group_name
  workspace_id         = azurerm_log_analytics_workspace.this.id
  application_type     = "web"
  daily_data_cap_in_gb = var.daily_data_cap_in_gb
  # Provider azurerm v5: la propiedad deprecada daily_data_cap_notifications_disabled
  # (false) fue removida en favor de daily_data_cap_notifications_enabled (true) --
  # mismo comportamiento, nombre invertido. Ver guia de migracion a v5, seccion
  # azurerm_application_insights. No estaba en el analisis original del issue #304;
  # se detecto al correr terraform validate (CA-5) contra el provider 5.0.1.
  daily_data_cap_notifications_enabled = true
  tags                                 = var.tags
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

  scopes                  = [azurerm_log_analytics_workspace.this.id]
  evaluation_frequency    = "PT1H"
  window_duration         = "P1D"
  auto_mitigation_enabled = true

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

# Alerta 2: pico de excepciones >50 en 5 minutos correlacionadas a un request con status 500
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "exception_spike" {
  name                = "${var.name}-exception-spike"
  resource_group_name = var.resource_group_name
  location            = var.location
  description         = "Pico de excepciones con respuesta 500 detectado - posible funcion en loop de errores generando costos"
  severity            = 1
  enabled             = true

  scopes               = [azurerm_application_insights.this.id]
  evaluation_frequency = "PT5M"
  window_duration      = "PT5M"

  criteria {
    # exceptions no expone el status code: vive en requests. Se correlacionan por
    # operation_Id, el id de traza compartido entre el request y la excepcion que lo
    # hizo fallar. Ver CA-ADR-0009.
    query = <<-QUERY
      let operacionesCon500 =
          requests
          | where timestamp > ago(5m)
          | where resultCode == "500"
          | distinct operation_Id;
      exceptions
      | where timestamp > ago(5m)
      | where operation_Id in (operacionesCon500)
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

# Alerta 3: pico de invocaciones fallidas >50 en 5 minutos en triggers no-HTTP
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "non_http_failure_spike" {
  name                = "${var.name}-non-http-failure-spike"
  resource_group_name = var.resource_group_name
  location            = var.location
  description         = "Pico de invocaciones fallidas en triggers no-HTTP - posible consumidor en loop de reintentos generando costos"
  severity            = 1
  enabled             = true

  scopes               = [azurerm_application_insights.this.id]
  evaluation_frequency = "PT5M"
  window_duration      = "PT5M"

  criteria {
    # Los triggers no-HTTP reportan resultCode "0", valor no contractual: en vez de
    # comparar contra el, se descarta lo que SI es un status HTTP (100..599).
    # Ver CA-ADR-0009.
    query = <<-QUERY
      requests
      | where timestamp > ago(5m)
      | where success == false
      | where isnull(toint(resultCode)) or toint(resultCode) !between (100 .. 599)
      | summarize InvocacionesFallidas = count()
      | where InvocacionesFallidas > 50
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

# Alerta 4: excepciones de agentes de fondo (sin operation_Id) por rol, generalizada
# (issue #415, reemplaza projections_exception_spike -- MEF-ADR-0018 Rule of Three: 4
# roles y 2 familias de agentes de fondo -- daemon HotCold de Marten, DurabilityAgent/
# IAgentCommand de Wolverine -- comparten el mismo modo de falla sin requests. Ver
# CA-ADR-0009, actualizacion 2026-08-30 issue #415.
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "background_exception_spike" {
  name                = "${var.name}-background-exception-spike"
  resource_group_name = var.resource_group_name
  location            = var.location
  description         = "Pico de excepciones de agentes de fondo (daemon de Marten, DurabilityAgent de Wolverine) por rol - las dos alertas de borde HTTP son ciegas a este modo de falla porque se condicionan a requests"
  severity            = 1
  enabled             = true

  scopes                  = [azurerm_application_insights.this.id]
  evaluation_frequency    = "PT5M"
  window_duration         = "PT5M"
  auto_mitigation_enabled = true

  criteria {
    # operation_Id vacio es la senal distintiva de una excepcion nacida fuera de un
    # request HTTP (verificado en el experimento del issue #412: las 88 del daemon y
    # las 810 de Wolverine llevan operation_Id vacio; las excepciones de rutas HTTP,
    # incluidas las de smoke tests, lo llevan poblado). TimeGenerated se proyecta
    # explicito porque number_of_evaluation_periods > 1 requiere que la query exponga
    # una columna de tiempo (Terraform Registry, azurerm_monitor_scheduled_query_rules_alert_v2,
    # nota de `number_of_evaluation_periods`). El umbral vive en el bloque `criteria`
    # (metric_measure_column + dimension), no embebido en la query con `| where N > 15`
    # como en las alertas hermanas: el motor necesita evaluar los failing periods por
    # cada valor de cloud_RoleName de forma independiente. Ver CA-ADR-0009.
    query = <<-QUERY
      exceptions
      | where timestamp > ago(10m)
      | where isempty(operation_Id)
      | summarize N = count() by cloud_RoleName, TimeGenerated = bin(timestamp, 5m)
    QUERY

    time_aggregation_method = "Maximum"
    operator                = "GreaterThan"
    threshold               = 15
    metric_measure_column   = "N"

    dimension {
      name     = "cloud_RoleName"
      operator = "Include"
      values   = ["*"]
    }

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 2
      number_of_evaluation_periods             = 2
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

output "log_analytics_workspace_id" {
  description = "ID del Log Analytics Workspace. Lo consume el modulo opt-in container-app-environment (MEF-ADR-0034 seccion 8, issue #234) para no crear un segundo workspace redundante (ADR-0009 local, control de costos de Application Insights)"
  value       = azurerm_log_analytics_workspace.this.id
}
