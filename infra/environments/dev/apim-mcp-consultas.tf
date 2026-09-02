# Servidor MCP de consultas detras de APIM (issue #558, migrado a la interfaz canonica de
# Mefisto por issue #575): gate de identidad del flujo Connect de AuthKit en el borde, distinto de
# la politica GLOBAL de LOGIN (apim.tf) y del patron apim-function-api (apim-dominio-*.tf) que
# hereda esa global con <base/>. Contexto completo del porque en
# infra/modules/apim-mcp-api/main.tf.
#
# Archivo propio y no apim.tf/mcp-consultas.tf: mismo razonamiento que apim-dominio-*.tf -- un
# archivo aparte por API nueva evita que dos scaffolds concurrentes choquen. Aditivo (CA-6): NO
# toca apim.tf (la instancia APIM y su politica global se crean una sola vez) ni apim-mcp-prm.tf
# (la API compartida del PRM tambien se crea una sola vez, issue #575).
#
# Issue #575: los locals mcp_consultas_prm_path/mcp_consultas_prm_url/
# mcp_consultas_authorization_server_url se retiran de aqui -- el primero y el segundo ahora los
# arma el modulo (local.resource_uri/local.prm_url en infra/modules/apim-mcp-api, a partir de
# gateway_url + path); el literal del dominio AuthKit deja de vivir en este archivo y pasa a
# var.mcp_authorization_server_url (apim-mcp-prm.tf), compartido por todos los servidores MCP.

locals {
  # Path bajo el gateway para el endpoint del protocolo MCP (streamable HTTP) de este servidor.
  # Pasa tal cual como "api_name" y "path" a module.apim_mcp_consultas (mismo valor, patron ya
  # usado en apim-dominio-*.tf) y como sufijo de la operacion GET sobre la API compartida del PRM.
  mcp_consultas_path = "mcp-consultas"
}

module "apim_mcp_consultas" {
  source = "../../modules/apim-mcp-api"

  api_management_name = module.api_management.name
  resource_group_name = module.resource_group.name
  gateway_url         = module.api_management.gateway_url

  api_name     = local.mcp_consultas_path
  display_name = "MCP Consultas"
  path         = local.mcp_consultas_path

  function_app_id = module.function_app_mcp_consultas.id
  # Hostname COMPUTADO por Azure, no "func-<name>.azurewebsites.net" armado a mano: los apps
  # nuevos reciben hostnames regionalizados y el backend de APIM apuntaria a un host inexistente
  # (revision infra-reviewer; el modulo function-app ya documenta esta regla en su output).
  function_app_default_hostname = module.function_app_mcp_consultas.default_hostname

  authorization_server_url = var.mcp_authorization_server_url
  mcp_prm_api_name         = azurerm_api_management_api.mcp_prm.name

  tags = local.tags
}
