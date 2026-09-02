# Servidor MCP de consultas detras de APIM (issue #558): gate de identidad del flujo Connect de
# AuthKit en el borde, distinto de la politica GLOBAL de LOGIN (apim.tf) y del patron
# apim-function-api (apim-dominio-*.tf) que hereda esa global con <base/>. Contexto completo del
# porque en infra/modules/apim-mcp-api/main.tf.
#
# Archivo propio y no apim.tf/mcp-consultas.tf: mismo razonamiento que apim-dominio-*.tf -- un
# archivo aparte por API nueva evita que dos scaffolds concurrentes choquen. Aditivo (CA-6): NO
# toca apim.tf (la instancia APIM y su politica global se crean una sola vez).

locals {
  # Host compartido de las tres piezas que MEF-ADR-0032/issue #558 (decision #4) exige que
  # coincidan byte a byte: <audience> de la politica MCP, campo "resource" del documento PRM y
  # Resource Indicator del dashboard WorkOS (manual, checklist del PR). trimsuffix defiende
  # contra que gateway_url venga con o sin "/" final -- ninguno de los locals de abajo debe
  # llevar una barra de mas.
  apim_gateway_host = trimsuffix(module.api_management.gateway_url, "/")

  # Path bajo el gateway para el endpoint del protocolo MCP (streamable HTTP). Pasa tal cual como
  # "name" a module.apim_mcp_consultas (mismo valor = api_name = path, patron ya usado en
  # apim-dominio-*.tf).
  mcp_consultas_path = "mcp-consultas"

  # Mismo valor que se fija en Mcp__ResourceUri (mcp-consultas.tf) y en <audience> de la politica
  # (modulo apim-mcp-api). Sin path adicional: la Function App YA no expone su propio hostname
  # como identidad publica, ahora es la URL de APIM (issue #558 decision #3).
  mcp_consultas_resource_uri = "${local.apim_gateway_host}/${local.mcp_consultas_path}"

  # PRM anonimo: replica EXACTA de lo que UriMetadataRecursoProtegido deriva en runtime a partir
  # de Mcp__ResourceUri -- new Uri(Recurso, "/api/.well-known/oauth-protected-resource") es una
  # ruta ABSOLUTA que reemplaza cualquier path de Recurso, por eso este valor NO cuelga de
  # mcp_consultas_path (son dos APIs de gateway hermanas, no anidadas -- ver el modulo).
  mcp_consultas_prm_path = "api/.well-known/oauth-protected-resource"
  mcp_consultas_prm_url  = "${local.apim_gateway_host}/${local.mcp_consultas_prm_path}"

  # Issue #561 (hermano de #560, app-side): dominio AuthKit del entorno contra el que
  # module.apim_mcp_consultas arma workos_issuer/workos_openid_config_url. Mismo valor,
  # verificado en vivo 2026-09-01, que AuthorizationServerAuthKit en Program.cs de
  # Bitakora.ControlAsistencia.Mcp.Consultas (issue #560) -- si diverge, el validate-jwt de esta
  # politica y el validador del propio Function App quedan mirando issuers distintos. Sin barra
  # final (local.workos_issuer del modulo la recorta de todos modos, pero se fija asi por
  # consistencia byte a byte con el discovery doc).
  mcp_consultas_authorization_server_url = "https://marvelous-polaroid-97-staging.authkit.app"
}

module "apim_mcp_consultas" {
  source = "../../modules/apim-mcp-api"

  api_management_name = module.api_management.name
  resource_group_name = module.resource_group.name

  name         = local.mcp_consultas_path
  display_name = "MCP Consultas"

  function_app_id = module.function_app_mcp_consultas.id
  # Hostname COMPUTADO por Azure, no "func-<name>.azurewebsites.net" armado a mano: los apps
  # nuevos reciben hostnames regionalizados y el backend de APIM apuntaria a un host inexistente
  # (revision infra-reviewer; el modulo function-app ya documenta esta regla en su output).
  function_app_default_hostname = module.function_app_mcp_consultas.default_hostname

  resource_audience = local.mcp_consultas_resource_uri
  prm_path          = local.mcp_consultas_prm_path
  prm_url           = local.mcp_consultas_prm_url

  authorization_server_url = local.mcp_consultas_authorization_server_url

  tags = local.tags
}
