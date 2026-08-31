# API del dominio control-horas detras del gateway APIM (MEF-ADR-0032, issue #335). Aditivo (CA-6):
# agregar este archivo nunca re-crea la instancia APIM de apim.tf. No lo regeneres si ya existe.

module "apim_api_control_horas" {
  source = "../../modules/apim-function-api"

  api_management_name = module.api_management.name
  resource_group_name = module.resource_group.name

  api_name     = "control-horas"
  display_name = "ControlHoras"
  path         = "control-horas"

  function_app_name                = module.function_app_control_horas.name
  function_app_resource_group_name = module.resource_group.name

  tags = local.tags
}
