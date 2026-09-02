# Terraform del servidor MCP de Comandos (MEF-ADR-0047, MEF-ADR-0048): Service Plan, Storage
# Account y Function App dedicados, mismo patron que mcp-consultas.tf -- sin rol sobre Key Vault:
# este servidor es cliente HTTP puro de los Function Apps del BC (MEF-ADR-0047 decision 3), sin
# SERVICE_BUS_CONNECTION ni MartenConnectionString, y sus app settings Api__*__BaseUrl no llevan
# ninguna referencia @Microsoft.KeyVault.
#
# Archivo propio, mismo razonamiento que mcp-consultas.tf/dominio-*.tf: un archivo aparte por app
# nueva evita que dos scaffolds concurrentes choquen editando el mismo archivo. Comparte los
# locals/modules ya declarados en main.tf/variables.tf (module.resource_group, module.monitoring,
# local.prefix_short, local.tags, local.storage_data_roles). Este entorno todavia no tiene el
# esquema region+secuencia (issue #730 del harness): local.prefix_short es
# "${var.project_short}-${var.environment}", sin sufijo de region.
#
# La system key mcp_extension la genera y custodia el host de Functions en runtime (MEF-ADR-0047
# decision 5); no se provisiona por Terraform.

resource "random_string" "storage_suffix_mcp_comandos" {
  length  = 6
  special = false
  upper   = false
}

module "storage_mcp_comandos" {
  source              = "../../modules/storage"
  name                = "stmcpcomandos${var.environment}${random_string.storage_suffix_mcp_comandos.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

# Mismo patron que el hermano mcp-consultas.tf: Linux, SKU B1, always_on = true.
module "service_plan_mcp_comandos" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-mcp-comandos"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  always_on           = true
  tags                = local.tags
}

module "function_app_mcp_comandos" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-mcp-comandos"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_mcp_comandos.id
  storage_account_name           = module.storage_mcp_comandos.name
  app_insights_connection_string = module.monitoring.connection_string
  always_on                      = module.service_plan_mcp_comandos.always_on
  # Convencion Api:BaseUrl (el codigo del servidor la lee en ConfiguracionClientesHttp): una linea
  # por dominio ya scaffoldeado que este servidor consume. Agregar una tool nueva que consuma otro
  # dominio exige agregar aqui su linea a mano, igual que en el codigo.
  #
  # Identidad__* (MEF-ADR-0047 decision 6): valor interino por despliegue,
  # TODO(tenancy etapa b / identidad derivada del token). Reutiliza el mismo tenant/usuario de
  # smoke que ya siembran los smoke tests de dominio (issue #538/#556) y que ya consume
  # Mcp.Consultas -- el literal "*DEFAULT*" (JasperFx.StorageConstants.DefaultTenantId,
  # CA-ADR-0027) quedo vacio tras la purga de dev, asi que cualquier otro valor devuelve vacio en
  # silencio.
  # Mcp__* (MEF-ADR-0047 decision 7, MEF-ADR-0032 seccion 9): AuthorizationServer es el dominio
  # AuthKit real de este entorno WorkOS (el mismo que usa Mcp.Consultas, ver su Program.cs) -- un
  # unico AuthKit por organizacion, no por servidor MCP, por eso viene de la misma
  # var.mcp_authorization_server_url y nunca de un literal (issue #571, Mefisto Paso 4b). ResourceUri
  # ya NO esta en placeholder: la URL de APIM de este servidor la provisiona
  # module.apim_mcp_comandos (infra/environments/dev/apim-mcp-comandos.tf, issue #571), analogo a
  # apim-mcp-consultas.tf.
  app_settings = {
    Api__Programacion__BaseUrl  = "https://${module.function_app_programacion.default_hostname}"
    Api__Sedes__BaseUrl         = "https://${module.function_app_sedes.default_hostname}"
    Api__ControlHoras__BaseUrl  = "https://${module.function_app_control_horas.default_hostname}"
    Api__Colaboradores__BaseUrl = "https://${module.function_app_colaboradores.default_hostname}"

    Identidad__TenantIdInterino = "tenant-smoke"
    Identidad__UserIdInterino   = "smoke@bitakora.dev"

    Mcp__ResourceUri         = module.apim_mcp_comandos.resource_uri
    Mcp__AuthorizationServer = var.mcp_authorization_server_url
  }
  tags = local.tags
}

# Storage por identidad administrada (ADR-0025 decision #3 / CA-ADR-0026 decision #5):
# AzureWebJobsStorage se resuelve por identidad, no por connection string. Unico RBAC que este
# app necesita.
resource "azurerm_role_assignment" "storage_data_mcp_comandos" {
  for_each             = local.storage_data_roles
  scope                = module.storage_mcp_comandos.id
  role_definition_name = each.value
  principal_id         = module.function_app_mcp_comandos.principal_id
}
