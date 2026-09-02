# API compartida del PRM (Protected Resource Metadata, RFC 9728) para TODOS los servidores MCP
# del BC (issue #575, forma canonica de Mefisto 0.35.0 Paso 3c.2 -- harness#820 CA-2).
#
# Se declara UNA sola vez por entorno, igual que module.api_management en apim.tf: el path de
# gateway "well-known/oauth-protected-resource" es UNICO por instancia APIM (no por servidor),
# asi que un segundo servidor MCP (Comandos, #570/#571) NO puede declarar su propia API de PRM
# con ese mismo path -- colisionaria con la de Consultas. Cada servidor MCP agrega su PROPIA
# operacion "GET /{path}" sobre esta API compartida (RFC 9728 seccion 3.1, "Example with path
# component") desde infra/modules/apim-mcp-api (var.mcp_prm_api_name); este archivo NO se toca al
# agregar un servidor nuevo (aditivo, mismo criterio CA-6 que apim.tf).
#
# Politica de API SIN <base/> y SIN validate-jwt (anonima): reemplaza integramente la global de
# LOGIN, igual que la politica del protocolo de cada servidor (infra/modules/apim-mcp-api,
# decision #1 de #558) -- un cliente MCP sin token todavia debe poder leer el documento PRM antes
# de autenticarse (spec de autorizacion MCP). Las operaciones por-servidor heredan ESTA politica
# via <base/> y le agregan su propio rewrite-uri + backend (ver el modulo).

variable "mcp_authorization_server_url" {
  description = "Dominio AuthKit del entorno (issue #575, Mefisto Paso 3c.3): unico valor compartido por TODOS los servidores MCP del BC -- un solo AuthKit por organizacion, no por servidor. Alimenta var.authorization_server_url de cada module.apim_mcp_* (issuer/openid-config de validate-jwt) y Mcp__AuthorizationServer del Function App correspondiente. Sin default: se fija via TF_VAR_mcp_authorization_server_url en CI (GitHub variable WORKOS_AUTHORIZATION_SERVER_URL, dominio publico, MEF-ADR-0032 seccion 6)."
  type        = string
}

# CA-4 (issue #575) -- gate VERIFICADO en rojo: APIM rechaza un path de API que EMPIEZA con punto.
# El apply de CI (Infra CD run 33662634923, 2026-09-02) fallo creando esta API con
# path = ".well-known/oauth-protected-resource": `400 ValidationError: One or more fields contain
# incorrect values`, sin nombrar el campo. La misma forma de recurso con "api/.well-known/..."
# (punto a MITAD de path, version #558) siempre aplico bien, y la doc REST de Api - Create Or
# Update solo declara minLength 0 / maxLength 400 para `path`: la regla es server-side y no esta
# documentada. `terraform validate`/`plan` no la detectan. Salida adoptada (la que este comentario
# ya anticipaba): path SIN el punto inicial. El <rewrite-uri> de cada operacion por servidor
# (infra/modules/apim-mcp-api) sigue reescribiendo al "/api/.well-known/oauth-protected-resource"
# con punto que el codigo expone, asi que el backend no cambia. El PRM deja la ubicacion
# well-known de RFC 9728 seccion 3; los clientes MCP lo descubren por el WWW-Authenticate
# resource_metadata del 401 (RFC 9728 seccion 5.1), que local.prm_url del modulo arma con ESTE
# mismo segmento -- deben coincidir byte a byte. NUNCA anidar el PRM bajo el path de un servidor
# (ver cabecera). Desviacion frente a la forma canonica de Mefisto reportada en harness#827.
resource "azurerm_api_management_api" "mcp_prm" {
  name                  = "mcp-prm"
  resource_group_name   = module.resource_group.name
  api_management_name   = module.api_management.name
  revision              = "1"
  display_name          = "MCP - Protected Resource Metadata"
  path                  = "well-known/oauth-protected-resource"
  protocols             = ["https"]
  subscription_required = false
}

# Sin <base/>: reemplaza la politica global (que trae validate-jwt de LOGIN, modulo
# api-management). Sin validate-jwt: el PRM es explicitamente anonimo (RFC 9728). Las operaciones
# GET /{path} de cada servidor (infra/modules/apim-mcp-api, azurerm_api_management_api_operation_policy.prm)
# heredan este inbound/backend vacio via <base/> y agregan su propio rewrite-uri + backend.
resource "azurerm_api_management_api_policy" "mcp_prm" {
  api_name            = azurerm_api_management_api.mcp_prm.name
  api_management_name = module.api_management.name
  resource_group_name = module.resource_group.name

  xml_content = <<XML
<policies>
  <inbound>
  </inbound>
  <backend>
    <forward-request />
  </backend>
  <outbound>
  </outbound>
  <on-error>
  </on-error>
</policies>
XML
}

output "mcp_prm_api_name" {
  description = "Nombre de la API APIM compartida del PRM (var.mcp_prm_api_name de cada module.apim_mcp_*)"
  value       = azurerm_api_management_api.mcp_prm.name
}
