# Terraform del servidor MCP de consultas (issue #508).
#
# Primer servidor MCP remoto del BC (sesion planner 2026-08-29): uno por Bounded Context y por
# proposito, partido en Consultas/Comandos (CQS). Este es el de consultas (incremento 5a, solo
# lectura). Este archivo provisiona el shell del Function App; el codigo con la extension
# Microsoft.Azure.Functions.Worker.Extensions.Mcp llega con #502 y el workflow de deploy con #509.
#
# Archivo propio y no main.tf: mismo razonamiento que dominio-sedes.tf -- un archivo aparte por
# app nueva evita que dos scaffolds concurrentes choquen editando el mismo archivo. Comparte los
# locals/modules ya declarados en main.tf (module.resource_group, module.monitoring,
# local.prefix_short, local.tags, local.storage_data_roles).
#
# Este app es cliente HTTP puro de los dominios del BC: sin SERVICE_BUS_CONNECTION ni
# MartenConnectionString (no toca bus ni event store) y sin rol sobre Key Vault (sus app settings
# no llevan referencias @Microsoft.KeyVault). Los endpoints de lectura de los dominios son
# AuthorizationLevel.Anonymous; la system key mcp_extension la genera y custodia el host de
# Functions en el storage del propio app, no se provisiona por Terraform.

resource "random_string" "storage_suffix_mcp_consultas" {
  length  = 6
  special = false
  upper   = false
}

module "storage_mcp_consultas" {
  source              = "../../modules/storage"
  name                = "stmcpconsultas${var.environment}${random_string.storage_suffix_mcp_consultas.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

# Mismo patron que los hermanos (Linux, SKU B1, always_on = true -- ver el comentario extendido
# junto a module.service_plan_programacion en dominio-programacion.tf). Se descarto consumption
# para el piloto:
# las restricciones de la extension MCP por plan de hosting no estan declaradas en la doc de
# Learn (gate no verificado, anotado en el issue) y B1 es la ruta ya probada por los hermanos.
module "service_plan_mcp_consultas" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-mcp-consultas"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  always_on           = true
  tags                = local.tags
}

module "function_app_mcp_consultas" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-mcp-consultas"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_mcp_consultas.id
  storage_account_name           = module.storage_mcp_consultas.name
  app_insights_connection_string = module.monitoring.connection_string
  always_on                      = module.service_plan_mcp_consultas.always_on
  # Convencion Api:BaseUrl de los smoke tests (Api__X__BaseUrl como variable de entorno). El
  # codigo del servidor MCP (#502) consume estos settings; si cambian los nombres, cambiar en
  # ambos lados.
  app_settings = {
    Api__Programacion__BaseUrl  = "https://${module.function_app_programacion.default_hostname}"
    Api__Sedes__BaseUrl         = "https://${module.function_app_sedes.default_hostname}"
    Api__ControlHoras__BaseUrl  = "https://${module.function_app_control_horas.default_hostname}"
    Api__Colaboradores__BaseUrl = "https://${module.function_app_colaboradores.default_hostname}"

    # Identidad interina que el servidor MCP estampa como X-Tenant-Id/X-User-Id en cada request
    # saliente hacia los Function Apps (issue #539). Son settings obligatorios: sin ellos el
    # arranque del worker falla (fail-fast de ConfiguracionIdentidadTenant, mismo criterio que
    # Api__*__BaseUrl).
    #
    # Los valores NO son arbitrarios: apuntan a la particion donde de verdad hay datos tras el
    # flip a multi-tenancy (PR #545) y la purga de dev -- el tenant "tenant-smoke" que siembran
    # los smoke de dominio (issue #538), con el UserId de IdentidadDePrueba de ese mismo issue
    # (issue #556). El literal "*DEFAULT*" (JasperFx.StorageConstants.DefaultTenantId,
    # CA-ADR-0027) quedo vacio tras la purga: ya nadie escribe ahi. En la etapa (a) de tenancy
    # los dominios ignoran estos headers, pero al pasar a la etapa (b) (MEF-ADR-0028 seccion 4)
    # el TrustedHeadersTenantResolver los convierte en el tenant efectivo de cada consulta:
    # cualquier otro valor apuntaria a un tenant sin datos y las tools responderian vacio en
    # silencio. Interino consciente: #540 (tenant por usuario autenticado) reemplaza este valor
    # fijo.
    Tenant__Id     = "tenant-smoke"
    Tenant__UserId = "smoke@bitakora.dev"

    # Identidad publica de este servidor como OAuth protected resource (issue #554): alimenta el
    # documento PRM y el parametro resource_metadata del WWW-Authenticate. Setting obligatorio --
    # sin el, el arranque del worker falla (mismo fail-fast que Api__*__BaseUrl).
    #
    # Issue #558: deja de ser la URL propia de la Function App (el host de Functions no puede
    # gatear las tool calls de McpToolTrigger, ver AutorizacionMcpMiddleware.cs) y pasa a ser la
    # URL de APIM del endpoint del protocolo -- el mismo local que arma <audience> en la politica
    # de module.apim_mcp_consultas (apim-mcp-consultas.tf). Los tres valores (este setting, el
    # campo "resource" del PRM y el Resource Indicator del dashboard WorkOS) deben coincidir
    # byte a byte (MEF-ADR-0032, decision #4 del issue).
    Mcp__ResourceUri = local.mcp_consultas_resource_uri
  }
  tags = local.tags
}

# Storage por identidad administrada (ADR-0025 decision #3 / CA-ADR-0026 decision #5):
# AzureWebJobsStorage se resuelve por identidad, no por connection string. Unico RBAC que este
# app necesita.
resource "azurerm_role_assignment" "storage_data_mcp_consultas" {
  for_each             = local.storage_data_roles
  scope                = module.storage_mcp_consultas.id
  role_definition_name = each.value
  principal_id         = module.function_app_mcp_consultas.principal_id
}
