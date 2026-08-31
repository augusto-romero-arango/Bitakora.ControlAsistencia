# Terraform del dominio Programacion (issue #221).
#
# Archivo propio, extraido de main.tf: los bloques de Programacion/ControlHoras vivian ahi porque
# asi se scaffoldearon originalmente, antes de que existiera la convencion de un archivo propio
# por dominio que introdujeron Colaboradores/Sedes (issue #360/#455). Terraform evalua todos los
# .tf del directorio del entorno como un unico root module (no hay subdirectorios que recorrer),
# asi que separar estos bloques no cambia ninguna direccion de recurso
# (module.function_app_programacion, module.service_plan_programacion, etc. quedan identicas) --
# terraform plan no deberia mostrar diff por este cambio. Comparte sin cambios las referencias a
# locals/modules declarados en main.tf (module.resource_group, module.key_vault,
# module.monitoring, local.prefix_short, local.tags, local.storage_data_roles,
# local.service_bus_connection_kv_ref, local.marten_connection_kv_ref).

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

# Un Service Plan por dominio (ADR-0020): aisla el computo de cada dominio. Mismas caracteristicas
# que el plan compartido anterior: Linux, SKU B1.
#
# always_on = true (issue #400): en un plan Basic dedicado la VM se factura 24/7 este o no la app
# dormida (doc oficial "Cost of App Service plans"), asi que apagar Always On no ahorra nada y
# ademas interrumpe el poll en background del agente de durabilidad de Wolverine
# (DurabilityMode.Solo, MEF-ADR-0020). os_type y worker_count no se pasan explicitos: sus defaults
# ("Linux", 1) replican el comportamiento previo sin diff. Mismo patron que
# ControlHoras/Colaboradores/Sedes.
module "service_plan_programacion" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-programacion"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  always_on           = true
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
  always_on                      = module.service_plan_programacion.always_on
  app_settings = {
    SERVICE_BUS_CONNECTION = local.service_bus_connection_kv_ref
    DOMINIO                = "programacion"
    MartenConnectionString = local.marten_connection_kv_ref
  }
  tags = local.tags
}

# RBAC data-plane (ADR-0024 decision #6): la managed identity necesita "Key Vault Secrets User"
# para resolver las referencias @Microsoft.KeyVault(...) de sus app settings.
resource "azurerm_role_assignment" "kv_secrets_user_programacion" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.function_app_programacion.principal_id
}

# Storage por identidad administrada (ADR-0025 decision #3): AzureWebJobsStorage no puede ir por
# referencia de Key Vault (el runtime necesita el storage al arrancar, antes de resolver
# referencias). La managed identity necesita los tres roles de datos de Storage sobre su propia
# cuenta.
resource "azurerm_role_assignment" "storage_data_programacion" {
  for_each             = local.storage_data_roles
  scope                = module.storage_programacion.id
  role_definition_name = each.value
  principal_id         = module.function_app_programacion.principal_id
}
