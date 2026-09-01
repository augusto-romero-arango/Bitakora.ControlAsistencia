# Modulo apim-mcp-api (issue #558, MEF-ADR-0032 variante MCP/Connect): expone un servidor MCP
# (McpToolTrigger del worker, paquete del host de Functions) detras del gateway APIM con la
# politica de identidad del flujo Connect de AuthKit -- DISTINTA de la politica GLOBAL de LOGIN
# (modulo api-management) y del modulo apim-function-api (que hereda esa global con <base/>).
#
# Por que un modulo aparte y no una variante de apim-function-api:
#   - La politica NO lleva <base/> en NINGUNA seccion (issue #558 decision #1): reemplaza la
#     global en vez de heredarla, porque la global valida tokens de LOGIN con
#     required-claims client_id (fijo) y sin <audiences> -- los tokens Connect traen un client_id
#     dinamico (Dynamic Client Registration) pero SI traen `aud` = Resource Indicator, asi que
#     aca se valida con <audiences> en vez de required-claims.
#   - El backend real del protocolo es /runtime/webhooks/mcp (lo sirve el paquete del host de
#     Functions), NO /api/<algo> como los backends HTTP normales de apim-function-api.
#   - El PRM (RFC 9728) debe ser alcanzable ANONIMAMENTE via APIM en un path fijo
#     (/api/.well-known/oauth-protected-resource) que NO cuelga del path del servidor MCP: el
#     propio codigo (UriMetadataRecursoProtegido) lo deriva con una ruta ABSOLUTA sobre el host
#     de Mcp__ResourceUri, reemplazando cualquier path existente -- por eso este modulo declara
#     DOS azurerm_api_management_api (protocolo + PRM) en vez de una sola con dos operaciones:
#     los paths de gateway son estructuralmente distintos ("<name>" vs
#     "api/.well-known/oauth-protected-resource"), no un prefijo compartido.
#
# Consistencia byte a byte (issue #558 decision #4): var.resource_audience y var.prm_url los
# arma el CALLER a partir del MISMO module.api_management.gateway_url que consume
# Mcp__ResourceUri en el Function App -- este modulo nunca reconstruye esos strings por su cuenta
# para no arriesgar un byte de diferencia entre <audience>, el `resource` del PRM y el Resource
# Indicator de WorkOS (manual, dashboard).

# Requerido en ESTE modulo (no alcanza con declararlo en el root, providers.tf): un modulo hijo
# que usa un recurso de un provider fuera del namespace hashicorp/ necesita su propio
# required_providers con el source explicito, o Terraform infiere hashicorp/azapi (inexistente)
# en vez de Azure/azapi.
terraform {
  required_providers {
    azapi = {
      source  = "Azure/azapi"
      version = "~> 2.0"
    }
  }
}

variable "api_management_name" {
  description = "Nombre de la instancia APIM (module.api_management.name)"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group de la instancia APIM (los recursos hijos de este modulo viven ahi: backends, named value, apis, policies)"
  type        = string
}

variable "name" {
  description = "Nombre base de este servidor MCP en kebab-case (ej. 'mcp-consultas'). Se usa como nombre interno y como path de gateway de la API del protocolo, y como prefijo del named value de la key."
  type        = string
}

variable "display_name" {
  description = "Nombre legible del servidor MCP (aparece en el portal del desarrollador)"
  type        = string
}

variable "function_app_id" {
  description = "ID completo del Function App backend (azurerm_linux_function_app.id / module.function_app_*.id) -- requerido por azapi_resource_action para leer la system key mcp_extension via listkeys (issue #558 decision #6: azurerm_function_app_host_keys no la expone)."
  type        = string
}

# Revision infra-reviewer: el hostname llega COMPUTADO desde el caller
# (module.function_app_*.default_hostname), no concatenado como "<name>.azurewebsites.net". El
# propio modulo function-app lo documenta asi ("usarlo en vez de concatenar name +
# .azurewebsites.net protege contra hostnames regionalizados"): Azure ya asigna hostnames
# regionalizados del tipo <name>-<hash>.<region>-01.azurewebsites.net a apps nuevas, y con el
# hostname adivinado el backend apuntaria a un host inexistente -- un fallo que ni
# `terraform validate` ni el plan detectan, solo el 404 en runtime de CA-4/CA-6.
variable "function_app_default_hostname" {
  description = "Hostname publico COMPUTADO del Function App backend (module.function_app_*.default_hostname, sin esquema). No lo construyas concatenando el nombre con 'azurewebsites.net': Azure asigna hostnames regionalizados."
  type        = string
}

variable "resource_audience" {
  description = "Valor EXACTO que viaja como <audience> en validate-jwt (issue #558 decision #1/#4): DEBE coincidir byte a byte con el app setting Mcp__ResourceUri del Function App y con el campo 'resource' del documento PRM. El caller lo arma con module.api_management.gateway_url + el path de esta API (mismo valor que var.name)."
  type        = string
}

variable "prm_path" {
  description = "Path bajo el gateway APIM donde vive el PRM anonimo. Fijo por el codigo (UriMetadataRecursoProtegido deriva '/api/.well-known/oauth-protected-resource' como ruta ABSOLUTA sobre el host de Mcp__ResourceUri) -- no cambiar sin cambiar tambien FunctionEndpoint.Ruta en Bitakora.ControlAsistencia.Mcp.Consultas."
  type        = string
  default     = "api/.well-known/oauth-protected-resource"
}

variable "prm_url" {
  description = "URL publica COMPLETA del PRM via APIM (issue #558 decision #2/#4): el caller la arma con el MISMO module.api_management.gateway_url + var.prm_path. Se usa en el WWW-Authenticate (resource_metadata) que emite on-error cuando validate-jwt rechaza el token."
  type        = string
}

variable "authorization_server_client_id" {
  description = "Client ID de AuthKit contra el que se arma el issuer/openid-config de esta politica (MEF-ADR-0032 seccion 8, B5: issuer client-specific, NUNCA 'https://api.workos.com' a secas). Default = el mismo valor YA verificado en vivo y hardcodeado en Program.cs de Bitakora.ControlAsistencia.Mcp.Consultas (AuthorizationServerAuthKit, issue #554) -- no el var.workos_client_id de LOGIN del modulo api-management, que es un proyecto/uso distinto."
  type        = string
  default     = "client_01M1CKPECJ5DBRMS3ZVFRQW8GW"
}

variable "protocol_methods" {
  description = "Verbos HTTP wildcard del endpoint del protocolo MCP (streamable HTTP): POST para las llamadas RPC, GET para streams servidor-iniciados, DELETE para terminacion de sesion (issue #558 decision #5, opcional pero incluido: costo marginal bajo)."
  type        = list(string)
  default     = ["POST", "GET", "DELETE"]
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

locals {
  # Raiz del backend: base-url SIN path. El path lo aporta <rewrite-uri> en cada politica (ver el
  # comentario de azurerm_api_management_backend.protocol).
  function_app_base_url = "https://${var.function_app_default_hostname}"

  workos_issuer            = "https://api.workos.com/user_management/${var.authorization_server_client_id}"
  workos_openid_config_url = "${local.workos_issuer}/.well-known/openid-configuration"
}

# ---- System key mcp_extension (issue #558 decision #6, gate NO VERIFICADO por el planner) ----
#
# azurerm_function_app_host_keys (data source) NO expone la system key mcp_extension -- los
# unicos atributos confirmados contra el provider azurerm son default/primary/event_grid/
# signalr/durabletask/webpubsub/blobs. Se lee con el listkeys nativo de ARM sobre el subrecurso
# host/default (Azure REST API "WebApps ListHostKeys"), via azapi_resource_action.
#
# sensitive_response_export_values (a diferencia de response_export_values) marca el atributo
# sensitive_output como Sensitive en el schema del provider: la CLI de Terraform lo redacta en
# plan/apply. El STATE sigue guardando el valor en claro de todos modos (mismo comportamiento que
# default_function_key en el modulo apim-function-api, MEF-ADR-0025) -- por eso, igual que ese
# modulo, esta key NUNCA se expone en un output de este modulo.
resource "azapi_resource_action" "function_host_keys" {
  type        = "Microsoft.Web/sites/host@2023-12-01"
  resource_id = "${var.function_app_id}/host/default"
  action      = "listkeys"
  method      = "POST"

  sensitive_response_export_values = {
    mcp_extension_key = "systemKeys.mcp_extension"
  }
}

resource "azurerm_api_management_named_value" "mcp_extension_key" {
  name                = "${var.name}-mcp-extension-key"
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  display_name        = "${var.name}-mcp-extension-key"
  value               = azapi_resource_action.function_host_keys.sensitive_output.mcp_extension_key
  secret              = true
}

# ---- API del PROTOCOLO MCP: valida el token Connect y reenvia a /runtime/webhooks/mcp ----

# Revision infra-reviewer: la url del backend es la RAIZ del host, sin "/runtime/webhooks/mcp".
# set-backend-service "changes the backend service BASE URL of the incoming request"
# (learn.microsoft.com/azure/api-management/set-backend-service-policy) y el template de
# <rewrite-uri> es el path que se le anexa a esa base (rewrite-uri-policy, ejemplos 1-3). Con la
# base terminando en /runtime/webhooks/mcp Y el rewrite del mismo path, el backend habria
# recibido /runtime/webhooks/mcp/runtime/webhooks/mcp -> 404 en toda tool call autenticada
# (CA-6), un fallo invisible para terraform validate y para el plan. El path vive en UN solo
# lugar: el <rewrite-uri> de la politica.
resource "azurerm_api_management_backend" "protocol" {
  name                = "${var.name}-protocol-backend"
  resource_group_name = var.resource_group_name
  api_management_name = var.api_management_name
  protocol            = "http"
  url                 = local.function_app_base_url

  credentials {
    header = {
      "x-functions-key" = "{{${azurerm_api_management_named_value.mcp_extension_key.name}}}"
    }
  }
}

resource "azurerm_api_management_api" "protocol" {
  name                  = var.name
  resource_group_name   = var.resource_group_name
  api_management_name   = var.api_management_name
  revision              = "1"
  display_name          = var.display_name
  path                  = var.name
  protocols             = ["https"]
  subscription_required = false
}

# Wildcard por verbo (mismo patron B11 de apim-function-api): el cliente MCP llama exactamente a
# la raiz de esta API (sin subpaths propios del protocolo), pero se declara "/*" -- nunca "/" a
# secas -- para no depender de que APIM matchee un template exacto sin barra. El <rewrite-uri> de
# la politica descarta cualquier sufijo capturado por el wildcard: el backend SIEMPRE recibe
# exactamente /runtime/webhooks/mcp, sea cual sea lo que el cliente haya puesto de mas.
resource "azurerm_api_management_api_operation" "protocol" {
  for_each = toset(var.protocol_methods)

  operation_id        = "${lower(each.value)}-mcp-protocol"
  api_name            = azurerm_api_management_api.protocol.name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  display_name        = "${each.value} protocolo MCP"
  method              = each.value
  url_template        = "/*"
}

# Politica SIN <base/> en ninguna seccion (issue #558 decision #1): reemplaza integramente la
# global. on-error emite el WWW-Authenticate con resource_metadata apuntando al PRM via APIM
# (decision #2) sin condicionar por status code -- esta API solo hace dos cosas (validar el JWT y
# reenviar al backend del protocolo), asi que cualquier error que llegue a on-error es, en la
# practica, un rechazo de validate-jwt (que ya fijo el 401 via failed-validation-httpcode).
# Orden de hijos de <validate-jwt> (XSD de APIM, learn.microsoft.com/azure/api-management/
# validate-jwt-policy: "set the policy's elements and child elements in the order provided"):
# openid-config -> issuer-signing-keys -> decryption-keys -> audiences -> issuers -> required-claims.
# <audiences> va ANTES de <issuers>. La nota B6 del modulo api-management omite audiences porque
# AuthKit no emite `aud` (B4); aqui si hay `aud` (token Connect) y el orden invertido hizo fallar
# el apply con un 400 ValidationError sin detalle (run 33566692118) que ni validate ni plan detectan.
resource "azurerm_api_management_api_policy" "protocol" {
  api_name            = azurerm_api_management_api.protocol.name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name

  xml_content = <<XML
<policies>
  <inbound>
    <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Unauthorized." output-token-variable-name="jwt">
      <openid-config url="${local.workos_openid_config_url}" />
      <audiences>
        <audience>${var.resource_audience}</audience>
      </audiences>
      <issuers>
        <issuer>${local.workos_issuer}</issuer>
      </issuers>
    </validate-jwt>
    <rewrite-uri template="/runtime/webhooks/mcp" copy-unmatched-params="true" />
    <set-backend-service backend-id="${azurerm_api_management_backend.protocol.name}" />
  </inbound>
  <backend>
    <forward-request />
  </backend>
  <outbound>
  </outbound>
  <on-error>
    <set-header name="WWW-Authenticate" exists-action="override">
      <value>Bearer error="invalid_token", resource_metadata="${var.prm_url}"</value>
    </set-header>
  </on-error>
</policies>
XML
}

# ---- API del PRM: anonima, reenvia a /api/.well-known/oauth-protected-resource ----
#
# Sin validate-jwt (spec de autorizacion MCP: un cliente sin token todavia debe poder leer este
# documento). El Function endpoint YA es AuthorizationLevel.Anonymous del lado del worker
# (FunctionEndpoint.cs) -- este backend tampoco inyecta la system key, no hace falta.

# Misma forma que el backend del protocolo (raiz + <rewrite-uri> en la politica): con la base
# terminando en el path del PRM y la operacion declarada como "/", el backend habria recibido el
# path con una barra final pegada. El rewrite explicito hace la ruta determinista.
resource "azurerm_api_management_backend" "prm" {
  name                = "${var.name}-prm-backend"
  resource_group_name = var.resource_group_name
  api_management_name = var.api_management_name
  protocol            = "http"
  url                 = local.function_app_base_url
}

resource "azurerm_api_management_api" "prm" {
  name                  = "${var.name}-prm"
  resource_group_name   = var.resource_group_name
  api_management_name   = var.api_management_name
  revision              = "1"
  display_name          = "${var.display_name} - PRM"
  path                  = var.prm_path
  protocols             = ["https"]
  subscription_required = false
}

resource "azurerm_api_management_api_operation" "prm" {
  operation_id        = "get-prm"
  api_name            = azurerm_api_management_api.prm.name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  display_name        = "GET PRM"
  method              = "GET"
  url_template        = "/"
}

resource "azurerm_api_management_api_policy" "prm" {
  api_name            = azurerm_api_management_api.prm.name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name

  xml_content = <<XML
<policies>
  <inbound>
    <rewrite-uri template="/${var.prm_path}" copy-unmatched-params="false" />
    <set-backend-service backend-id="${azurerm_api_management_backend.prm.name}" />
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

# Ningun output expone la system key mcp_extension (MEF-ADR-0025): vive solo en el named value
# secreto y en el state, nunca en una salida legible.

output "protocol_api_id" {
  description = "ID de la API APIM del endpoint del protocolo MCP"
  value       = azurerm_api_management_api.protocol.id
}

output "protocol_api_name" {
  description = "Nombre de la API APIM del endpoint del protocolo MCP"
  value       = azurerm_api_management_api.protocol.name
}

output "prm_api_id" {
  description = "ID de la API APIM del documento PRM anonimo (RFC 9728)"
  value       = azurerm_api_management_api.prm.id
}

output "prm_api_name" {
  description = "Nombre de la API APIM del documento PRM anonimo (RFC 9728)"
  value       = azurerm_api_management_api.prm.name
}
