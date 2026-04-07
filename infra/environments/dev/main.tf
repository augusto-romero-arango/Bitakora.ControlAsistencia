module "resource_group" {
  source   = "../../modules/resource-group"
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}


module "service_plan" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix}"
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
    "eventos-programacion" = {
      subscriptions = []
    }
  }
  tags                = local.tags
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
  source                            = "../../modules/function-app"
  name                              = "func-${local.prefix_short}-programacion"
  resource_group_name               = module.resource_group.name
  location                          = module.resource_group.location
  service_plan_id                   = module.service_plan.id
  storage_account_name              = module.storage_programacion.name
  storage_account_connection_string = module.storage_programacion.primary_connection_string
  storage_account_access_key        = module.storage_programacion.primary_access_key
  app_insights_connection_string    = module.monitoring.connection_string
  app_settings = {
    SERVICE_BUS_CONNECTION = module.service_bus.default_primary_connection_string
    DOMINIO                = "programacion"
    MartenConnectionString = "Host=${module.postgresql.server_fqdn};Database=${module.postgresql.database_name};Username=pgadmin;Password=${var.postgresql_admin_password};SSL Mode=Require"
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
  source                            = "../../modules/function-app"
  name                              = "func-${local.prefix_short}-control-horas"
  resource_group_name               = module.resource_group.name
  location                          = module.resource_group.location
  service_plan_id                   = module.service_plan.id
  storage_account_name              = module.storage_control_horas.name
  storage_account_connection_string = module.storage_control_horas.primary_connection_string
  storage_account_access_key        = module.storage_control_horas.primary_access_key
  app_insights_connection_string    = module.monitoring.connection_string
  app_settings = {
    SERVICE_BUS_CONNECTION = module.service_bus.default_primary_connection_string
    DOMINIO                = "control-horas"
    MartenConnectionString = "Host=${module.postgresql.server_fqdn};Database=${module.postgresql.database_name};Username=pgadmin;Password=${var.postgresql_admin_password};SSL Mode=Require"
  }
  tags = local.tags
}

