# Terraform del dominio ControlHoras (issue #221).
#
# Archivo propio, extraido de main.tf -- ver el comentario extendido en dominio-programacion.tf
# sobre por que este split no cambia ninguna direccion de recurso. Comparte sin cambios las
# referencias a locals/modules declarados en main.tf (module.resource_group, module.key_vault,
# module.monitoring, local.prefix_short, local.tags, local.storage_data_roles,
# local.service_bus_connection_kv_ref, local.marten_connection_kv_ref).

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

# Un Service Plan por dominio (ADR-0020): aisla el computo de cada dominio. Mismo patron que
# Programacion/Colaboradores/Sedes (Linux, SKU B1 -- el default del modulo).
#
# always_on = true (issue #400): ver el comentario extendido junto a
# module.service_plan_programacion en dominio-programacion.tf -- mismo razonamiento, un plan
# Basic dedicado factura la VM 24/7 independientemente de Always On.
module "service_plan_control_horas" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-control-horas"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  always_on           = true
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
  always_on                      = module.service_plan_control_horas.always_on
  app_settings = {
    SERVICE_BUS_CONNECTION = local.service_bus_connection_kv_ref
    DOMINIO                = "control-horas"
    MartenConnectionString = local.marten_connection_kv_ref
  }
  tags = local.tags
}

# RBAC data-plane (ADR-0024 decision #6): la managed identity necesita "Key Vault Secrets User"
# para resolver las referencias @Microsoft.KeyVault(...) de sus app settings.
resource "azurerm_role_assignment" "kv_secrets_user_control_horas" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.function_app_control_horas.principal_id
}

# Storage por identidad administrada (ADR-0025 decision #3): AzureWebJobsStorage no puede ir por
# referencia de Key Vault (el runtime necesita el storage al arrancar, antes de resolver
# referencias). La managed identity necesita los tres roles de datos de Storage sobre su propia
# cuenta.
resource "azurerm_role_assignment" "storage_data_control_horas" {
  for_each             = local.storage_data_roles
  scope                = module.storage_control_horas.id
  role_definition_name = each.value
  principal_id         = module.function_app_control_horas.principal_id
}
