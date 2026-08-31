# Terraform del dominio Colaboradores (issue #360).
#
# Archivo propio y no main.tf: un archivo aparte por dominio evita que dos scaffolds concurrentes
# choquen editando el mismo archivo -- convencion que tambien adoptaron Programacion/ControlHoras
# (dominio-programacion.tf, dominio-control-horas.tf) al extraerse de main.tf. Terraform evalua
# todos los .tf del directorio del entorno como un unico root module (no hay subdirectorios que
# recorrer), asi que este archivo comparte sin cambios las referencias a locals/modules ya
# declarados en main.tf (module.resource_group, module.key_vault, module.monitoring,
# local.prefix_short, local.tags, local.storage_data_roles, local.service_bus_connection_kv_ref,
# local.marten_connection_kv_ref).
#
# Divergencias deliberadas frente a la plantilla generica del harness (documentadas en
# CA-ADR-0026, "Custodia de secretos"): este proyecto usa un UNICO namespace de Service Bus y un
# UNICO app setting SERVICE_BUS_CONNECTION (sin la topologia interno/externo de MEF-ADR-0024 del
# harness), y Application Insights se configura con el valor directo de
# module.monitoring.connection_string (no una referencia de Key Vault, decision #7 de ese ADR).
# El modulo local infra/modules/service-plan ya acepta los cuatro inputs del contrato de
# MEF-ADR-0020 (sku_name, os_type, worker_count, always_on -- issue #400, cierra la deriva
# senalada aqui anteriormente): os_type y worker_count no se pasan explicitos porque sus
# defaults ("Linux", 1) replican el comportamiento previo sin diff.

resource "random_string" "storage_suffix_colaboradores" {
  length  = 6
  special = false
  upper   = false
}

module "storage_colaboradores" {
  source              = "../../modules/storage"
  name                = "stcolaboradores${var.environment}${random_string.storage_suffix_colaboradores.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

# Un Service Plan por dominio (ADR-0020): aisla el computo de cada dominio. Mismo patron que
# Programacion/ControlHoras (Linux, SKU B1 -- el default del modulo).
#
# always_on = true (issue #400): ver el comentario extendido junto a
# module.service_plan_programacion en dominio-programacion.tf -- mismo razonamiento, un plan
# Basic dedicado factura la VM 24/7 independientemente de Always On.
module "service_plan_colaboradores" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-colaboradores"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  always_on           = true
  tags                = local.tags
}

module "function_app_colaboradores" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-colaboradores"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_colaboradores.id
  storage_account_name           = module.storage_colaboradores.name
  app_insights_connection_string = module.monitoring.connection_string
  always_on                      = module.service_plan_colaboradores.always_on
  app_settings = {
    SERVICE_BUS_CONNECTION = local.service_bus_connection_kv_ref
    DOMINIO                = "colaboradores"
    MartenConnectionString = local.marten_connection_kv_ref
  }
  tags = local.tags
}

# RBAC data-plane (ADR-0024 decision #6 / CA-ADR-0026): la managed identity necesita
# "Key Vault Secrets User" para resolver las referencias @Microsoft.KeyVault(...) de sus app
# settings.
resource "azurerm_role_assignment" "kv_secrets_user_colaboradores" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.function_app_colaboradores.principal_id
}

# Storage por identidad administrada (ADR-0025 decision #3 / CA-ADR-0026 decision #5):
# AzureWebJobsStorage se resuelve por identidad, no por Key Vault.
resource "azurerm_role_assignment" "storage_data_colaboradores" {
  for_each             = local.storage_data_roles
  scope                = module.storage_colaboradores.id
  role_definition_name = each.value
  principal_id         = module.function_app_colaboradores.principal_id
}
