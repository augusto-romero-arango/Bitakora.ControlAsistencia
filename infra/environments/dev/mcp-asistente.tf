# Terraform del servidor MCP de Asistente (MEF-ADR-0047, MEF-ADR-0048): Service Plan, Storage
# Account y Function App dedicados, mismo patron que los Function Apps de dominio pero sin rol
# sobre Key Vault -- este servidor es cliente HTTP puro de los Function Apps del BC (MEF-ADR-0047
# decision 3), sin SERVICE_BUS_CONNECTION ni MartenConnectionString, y sus app settings
# Api__*__BaseUrl no llevan ninguna referencia @Microsoft.KeyVault.
#
# Comparte los locals/modules ya declarados en main.tf (module.resource_group, module.monitoring,
# local.prefix_short, local.tags, local.storage_data_roles). La system key mcp_extension la genera
# y custodia el host de Functions en runtime (MEF-ADR-0047 decision 5); no se provisiona por
# Terraform.

resource "random_string" "storage_suffix_mcp_asistente" {
  length  = 6
  special = false
  upper   = false
}

module "storage_mcp_asistente" {
  source              = "../../modules/storage"
  name                = "stmcpasistente${var.environment}${random_string.storage_suffix_mcp_asistente.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "service_plan_mcp_asistente" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-mcp-asistente"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  always_on           = true
  tags                = local.tags
}

module "function_app_mcp_asistente" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-mcp-asistente"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_mcp_asistente.id
  storage_account_name           = module.storage_mcp_asistente.name
  app_insights_connection_string = module.monitoring.connection_string
  always_on                      = module.service_plan_mcp_asistente.always_on
  log_analytics_workspace_id     = module.monitoring.log_analytics_workspace_id
  # Convencion Api:BaseUrl (el codigo del servidor la lee en ConfiguracionClientesHttp): una linea
  # por dominio ya scaffoldeado que este servidor consume. Agregar una tool nueva que consuma otro
  # dominio exige agregar aqui su linea a mano, igual que en el codigo.
  #
  # Identidad__* (MEF-ADR-0047 decision 6): valor interino por despliegue, TODO(tenancy etapa b /
  # identidad derivada del token). El BC esta en multi-tenant-header: el valor apunta al tenant
  # "tenant-smoke" con datos en dev (mismo criterio que los servidores MCP retirados en #806);
  # cualquier otro valor consultaria un tenant sin datos y las tools responderian vacio en silencio.
  #
  # Mcp__* (MEF-ADR-0047 decision 7, MEF-ADR-0032 seccion 9): AuthorizationServer es el dominio
  # AuthKit del entorno (var.mcp_authorization_server_url). ResourceUri lo resuelve el modulo
  # apim-mcp-api (apim-mcp-asistente.tf) y coincide byte a byte con el PRM y el <audiences> de la
  # politica dedicada.
  app_settings = {
    Api__Programacion__BaseUrl                  = "https://${module.function_app_programacion.default_hostname}"
    Api__Sedes__BaseUrl                         = "https://${module.function_app_sedes.default_hostname}"
    Api__ControlHoras__BaseUrl                  = "https://${module.function_app_control_horas.default_hostname}"
    Api__Colaboradores__BaseUrl                 = "https://${module.function_app_colaboradores.default_hostname}"
    Identidad__TenantIdInterino                 = "tenant-smoke"
    Identidad__UserIdInterino                   = "smoke@bitakora.dev"
    Identidad__OrganizationMembershipIdInterino = "om-smoke"
    Mcp__ResourceUri                            = module.apim_mcp_asistente.resource_uri
    Mcp__AuthorizationServer                    = var.mcp_authorization_server_url
  }
  tags = local.tags
}

# Storage por identidad administrada (MEF-ADR-0025 decision #3): AzureWebJobsStorage se resuelve
# por identidad, no por connection string -- mismo mecanismo que cada Function App de dominio.
resource "azurerm_role_assignment" "storage_data_mcp_asistente" {
  for_each             = local.storage_data_roles
  scope                = module.storage_mcp_asistente.id
  role_definition_name = each.value
  principal_id         = module.function_app_mcp_asistente.principal_id
}
