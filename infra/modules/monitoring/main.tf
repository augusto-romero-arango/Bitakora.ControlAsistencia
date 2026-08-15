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

# Alerta 2: pico de excepciones >50 en 5 minutos en el borde HTTP (patron de funcion en
# loop de errores), acotada a las excepciones cuya request termino en 500. Las violaciones
# de regla de negocio de un comando HTTP responden 409/404 sin excepcion persistida
# (CA-ADR-0030), asi que un 500 es siempre un fallo tecnico no manejado: es la unica clase
# de excepcion que se quiere vigilar aqui.
#
# Cubre SOLO el borde HTTP: los triggers de Service Bus reportan resultCode "0", no un
# status HTTP, asi que ninguna invocacion suya entra por este filtro. Su cobertura es la
# alerta 3.
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
    # La tabla exceptions no expone el status code: vive en requests. Se correlacionan
    # por operation_Id, que es el id de la traza compartida entre el request y la
    # excepcion que lo hizo fallar.
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

# Alerta 3: pico de invocaciones fallidas en triggers que no son HTTP -- en la practica,
# los consumidores de Service Bus. Es la contraparte de la alerta 2 para el lado del bus.
#
# Por que sobre invocaciones y no sobre la metrica DeadletteredMessages: esa metrica solo
# se desglosa por la dimension EntityName, que es la queue o el TOPIC -- Azure Monitor no
# expone metricas por subscription. Como la subscription "smoke-tests" vive dentro de los
# mismos topics de negocio (ver topics_config del ambiente), una alerta metrica sumaria su
# DLQ junto al de las subscriptions reales, sin forma de separarlos.
#
# Esta consulta no tiene ese problema y no necesita lista de exclusion: la subscription
# "smoke-tests" no tiene Function App consumidora, asi que no produce invocaciones. Sus
# dead letters los genera el propio proceso de smoke tests (CI), que no reporta a
# Application Insights. Lo que si cuenta -- y debe contar -- es una funcion real fallando
# al procesar un mensaje publicado por un smoke test: eso es un error del sistema.
#
# Ademas detecta antes que el DLQ, que solo aparece tras agotar max_delivery_count (10).
resource "azurerm_monitor_scheduled_query_rules_alert_v2" "service_bus_failure_spike" {
  name                = "${var.name}-servicebus-failure-spike"
  resource_group_name = var.resource_group_name
  location            = var.location
  description         = "Pico de invocaciones fallidas en consumidores de Service Bus - posible consumidor en loop de reintentos generando costos"
  severity            = 1
  enabled             = true

  scopes               = [azurerm_application_insights.this.id]
  evaluation_frequency = "PT5M"
  window_duration      = "PT5M"

  criteria {
    # Los triggers no-HTTP reportan resultCode "0" en vez de un status HTTP. En vez de
    # comparar contra "0" -- valor no contractual -- se descarta lo que SI es un status
    # HTTP: si resultCode no es un numero en 100..599, la invocacion no vino del borde
    # HTTP. success == false es el marcador de invocacion fallida comun a todo trigger.
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
