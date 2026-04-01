module "resource_group" {
  source   = "../../modules/resource-group"
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}

module "storage" {
  source              = "../../modules/storage"
  name                = "stca${var.environment}func"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "service_plan" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "Y1"
  tags                = local.tags
}

module "monitoring" {
  source              = "../../modules/monitoring"
  name                = local.prefix
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "service_bus" {
  source              = "../../modules/service-bus"
  name                = "sb-${local.prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku                 = "Standard"
  topics              = ["eventos-asistencias"]
  tags                = local.tags
}

module "function_apps" {
  for_each = toset(var.dominios)

  source                            = "../../modules/function-app"
  name                              = "func-${local.prefix}-${each.value}"
  resource_group_name               = module.resource_group.name
  location                          = module.resource_group.location
  service_plan_id                   = module.service_plan.id
  storage_account_name              = module.storage.name
  storage_account_connection_string = module.storage.primary_connection_string
  storage_account_access_key        = module.storage.primary_access_key
  app_insights_connection_string    = module.monitoring.connection_string
  app_settings = {
    SERVICE_BUS_CONNECTION = module.service_bus.default_primary_connection_string
    DOMINIO                = each.value
  }
  tags = local.tags
}
