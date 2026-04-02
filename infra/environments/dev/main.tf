module "resource_group" {
  source   = "../../modules/resource-group"
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}

resource "random_string" "storage_suffix" {
  length  = 6
  special = false
  upper   = false
}

module "storage" {
  source              = "../../modules/storage"
  name                = "stca${var.environment}${random_string.storage_suffix.result}"
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
  topics_config       = {}
  tags                = local.tags
}

