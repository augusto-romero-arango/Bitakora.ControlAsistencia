# Servidor MCP de asistente detras de APIM (issue #800, forma canonica de Mefisto Paso 4b tras la
# reconciliacion de #575): gate de identidad del flujo Connect de AuthKit en el borde, mismo
# patron que el pionero apim-mcp-consultas.tf. Contexto completo del porque en
# infra/modules/apim-mcp-api/main.tf.
#
# Archivo propio y no apim.tf/apim-mcp-prm.tf/mcp-asistente.tf: mismo razonamiento que
# apim-mcp-consultas.tf -- un archivo aparte por API nueva evita que dos scaffolds concurrentes
# choquen. Aditivo (issue #800 descripcion): NO toca apim.tf (la instancia APIM y su politica
# global se crean una sola vez), apim-mcp-prm.tf (la API compartida del PRM tambien se crea una
# sola vez, issue #575), mcp-consultas.tf ni apim-mcp-consultas.tf.

locals {
  # Path bajo el gateway para el endpoint del protocolo MCP (streamable HTTP) de este servidor.
  # Pasa tal cual como "api_name" y "path" a module.apim_mcp_asistente (mismo valor, patron ya
  # usado en apim-mcp-consultas.tf) y como sufijo de la operacion GET sobre la API compartida del
  # PRM.
  mcp_asistente_path = "mcp-asistente"
}

module "apim_mcp_asistente" {
  source = "../../modules/apim-mcp-api"

  api_management_name = module.api_management.name
  resource_group_name = module.resource_group.name
  gateway_url         = module.api_management.gateway_url

  api_name     = local.mcp_asistente_path
  display_name = "MCP Asistente"
  path         = local.mcp_asistente_path

  function_app_id = module.function_app_mcp_asistente.id
  # Hostname COMPUTADO por Azure, no "func-<name>.azurewebsites.net" armado a mano: los apps
  # nuevos reciben hostnames regionalizados y el backend de APIM apuntaria a un host inexistente
  # (revision infra-reviewer; el modulo function-app ya documenta esta regla en su output).
  function_app_default_hostname = module.function_app_mcp_asistente.default_hostname

  authorization_server_url = var.mcp_authorization_server_url
  mcp_prm_api_name         = azurerm_api_management_api.mcp_prm.name

  tags = local.tags
}
