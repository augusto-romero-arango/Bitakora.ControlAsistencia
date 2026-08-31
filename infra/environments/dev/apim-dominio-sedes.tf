# API del dominio sedes detras del gateway APIM (MEF-ADR-0032, issue #335). Aditivo (CA-6):
# agregar este archivo nunca re-crea la instancia APIM de apim.tf. No lo regeneres si ya existe.

module "apim_api_sedes" {
  source = "../../modules/apim-function-api"

  api_management_name = module.api_management.name
  resource_group_name = module.resource_group.name

  api_name     = "sedes"
  display_name = "Sedes"
  path         = "sedes"

  function_app_name                = module.function_app_sedes.name
  function_app_resource_group_name = module.resource_group.name

  tags = local.tags
}
