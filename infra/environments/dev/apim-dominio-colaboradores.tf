# API del dominio colaboradores detras del gateway APIM (MEF-ADR-0032, issue #335). Aditivo (CA-6):
# agregar este archivo nunca re-crea la instancia APIM de apim.tf. No lo regeneres si ya existe.

module "apim_api_colaboradores" {
  source = "../../modules/apim-function-api"

  api_management_name = module.api_management.name
  resource_group_name = module.resource_group.name

  api_name     = "colaboradores"
  display_name = "Colaboradores"
  path         = "colaboradores"

  function_app_name                = module.function_app_colaboradores.name
  function_app_resource_group_name = module.resource_group.name

  tags = local.tags
}
