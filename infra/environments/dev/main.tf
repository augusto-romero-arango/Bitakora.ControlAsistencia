data "azurerm_client_config" "current" {}

module "resource_group" {
  source   = "../../modules/resource-group"
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}


# Un Service Plan por dominio (ADR-0020): aisla el computo de cada dominio.
# Mismas caracteristicas que el plan compartido anterior: Linux, SKU B1.
module "service_plan_programacion" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-programacion"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  tags                = local.tags
}

module "service_plan_control_horas" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-control-horas"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  tags                = local.tags
}

module "monitoring" {
  source              = "../../modules/monitoring"
  name                = local.prefix
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "postgresql" {
  source                 = "../../modules/postgresql"
  name                   = "psql-${local.prefix_short}"
  resource_group_name    = module.resource_group.name
  location               = "centralus" # eastus2 restringida para PostgreSQL Flexible Server
  zone                   = "2"         # zona asignada por Azure al crear el servidor
  administrator_login    = "pgadmin"
  administrator_password = var.postgresql_admin_password
  database_name          = "controlasistencias"
  tags                   = local.tags
}

module "service_bus" {
  source              = "../../modules/service-bus"
  name                = "sb-${local.prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku                 = "Standard"
  topics_config = {
    "programacion-turno-diario-solicitada" = {
      subscriptions = [
        {
          name   = "control-horas-escucha-programacion"
          filter = null
        },
        {
          name                = "smoke-tests"
          filter              = null
          default_message_ttl = "PT5M"
        }
      ]
    }
    "dia-calculado" = {
      subscriptions = [
        {
          name                = "smoke-tests"
          filter              = null
          default_message_ttl = "PT5M"
        }
      ]
    }
  }
  tags = local.tags
}

# Almacen general de secretos del BC (ADR-0025 decision #5). El nombre del
# Key Vault es un endpoint DNS publico (*.vault.azure.net), unico en TODO
# Azure (ADR-0021): requiere sufijo de unicidad global igual que PostgreSQL
# y Service Bus.
resource "random_string" "key_vault_suffix" {
  length  = 6
  special = false
  upper   = false
}

module "key_vault" {
  source              = "../../modules/key-vault"
  name                = "kv-${var.project_short}-${random_string.key_vault_suffix.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  tags                = local.tags
}

# Referencias versionless a Key Vault (ADR-0025 decision #2): el app setting
# lleva la referencia, no el valor. El valor lo siembra un admin (decision #6).
# Versionless (sin sufijo de version): toma la ultima al rotar el secreto.
locals {
  service_bus_connection_kv_ref = "@Microsoft.KeyVault(SecretUri=${module.key_vault.uri}secrets/service-bus-connection)"
  marten_connection_kv_ref      = "@Microsoft.KeyVault(SecretUri=${module.key_vault.uri}secrets/marten-connection)"
}

resource "random_string" "storage_suffix_programacion" {
  length  = 6
  special = false
  upper   = false
}

module "storage_programacion" {
  source              = "../../modules/storage"
  name                = "stprogramacion${var.environment}${random_string.storage_suffix_programacion.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "function_app_programacion" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-programacion"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_programacion.id
  storage_account_name           = module.storage_programacion.name
  app_insights_connection_string = module.monitoring.connection_string
  app_settings = {
    SERVICE_BUS_CONNECTION = local.service_bus_connection_kv_ref
    DOMINIO                = "programacion"
    MartenConnectionString = local.marten_connection_kv_ref
  }
  tags = local.tags
}

resource "random_string" "storage_suffix_control_horas" {
  length  = 6
  special = false
  upper   = false
}

module "storage_control_horas" {
  source              = "../../modules/storage"
  name                = "stcontrolhoras${var.environment}${random_string.storage_suffix_control_horas.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "function_app_control_horas" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-control-horas"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_control_horas.id
  storage_account_name           = module.storage_control_horas.name
  app_insights_connection_string = module.monitoring.connection_string
  app_settings = {
    SERVICE_BUS_CONNECTION = local.service_bus_connection_kv_ref
    DOMINIO                = "control-horas"
    MartenConnectionString = local.marten_connection_kv_ref
  }
  tags = local.tags
}

# RBAC data-plane (ADR-0024 decision #6): la managed identity de cada Function
# App necesita "Key Vault Secrets User" para resolver las referencias
# @Microsoft.KeyVault(...) de sus app settings.
resource "azurerm_role_assignment" "kv_secrets_user_programacion" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.function_app_programacion.principal_id
}

resource "azurerm_role_assignment" "kv_secrets_user_control_horas" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.function_app_control_horas.principal_id
}

# Storage por identidad administrada (ADR-0025 decision #3): AzureWebJobsStorage
# no puede ir por referencia de Key Vault (el runtime necesita el storage al
# arrancar, antes de resolver referencias). La managed identity de cada Function
# App necesita los tres roles de datos de Storage sobre su propia cuenta.
locals {
  storage_data_roles = toset([
    "Storage Blob Data Owner",
    "Storage Queue Data Contributor",
    "Storage Table Data Contributor",
  ])
}

resource "azurerm_role_assignment" "storage_data_programacion" {
  for_each             = local.storage_data_roles
  scope                = module.storage_programacion.id
  role_definition_name = each.value
  principal_id         = module.function_app_programacion.principal_id
}

resource "azurerm_role_assignment" "storage_data_control_horas" {
  for_each             = local.storage_data_roles
  scope                = module.storage_control_horas.id
  role_definition_name = each.value
  principal_id         = module.function_app_control_horas.principal_id
}

