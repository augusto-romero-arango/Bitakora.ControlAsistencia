# Modulo apim-mcp-api (issue #558/#561, MEF-ADR-0032 variante MCP/Connect): expone un servidor
# MCP (McpToolTrigger del worker, paquete del host de Functions) detras del gateway APIM con la
# politica de identidad del flujo Connect de AuthKit -- DISTINTA de la politica GLOBAL de LOGIN
# (modulo api-management) y del modulo apim-function-api (que hereda esa global con <base/>).
#
# Issue #575 (reconciliacion con Mefisto 0.35.0, harness#820 CA-2): la interfaz de variables y
# outputs de este modulo (api_management_name, resource_group_name, gateway_url, api_name,
# display_name, path, authorization_server_url, mcp_prm_api_name, tags; outputs resource_uri,
# prm_url, protocol_api_id, protocol_api_name) es la forma CANONICA que
# apim-gateway-scaffolder Paso 2b espera para que /install-apim pueda generar
# apim-mcp-<servidor>.tf de cualquier servidor futuro sin romper terraform validate. Lo que se
# conserva del pionero (#558/#561) son desviaciones DELIBERADAS frente al HCL de Mefisto, cada
# una documentada abajo en el punto del codigo donde aplica -- Mefisto es una reconstruccion NO
# VERIFICADA de este modulo y regresiono exactamente estos cuatro puntos:
#   1. function_app_id + function_app_default_hostname COMPUTADOS por el caller, nunca
#      "<name>.azurewebsites.net" concatenado a mano (ver var.function_app_default_hostname).
#   2. <audiences> ANTES de <issuers> en validate-jwt (ver azurerm_api_management_api_policy.protocol).
#   3. Backend del protocolo con URL RAIZ + <rewrite-uri> explicito, nunca el path en la base-url
#      del backend (ver azurerm_api_management_backend.protocol).
#   4. azapi_resource_action (evaluado en apply), nunca un data source, para leer la system key
#      mcp_extension (ver azapi_resource_action.function_host_keys).
#
# El PRM (RFC 9728) SI adopta la forma de Mefisto (issue #575, decision nueva frente a #558): deja
# de ser una API propia por servidor y pasa a ser UNA operacion "GET /{path}" mas sobre la API
# COMPARTIDA var.mcp_prm_api_name ("well-known/oauth-protected-resource" -- SIN punto inicial,
# APIM lo rechaza con 400, ver el comentario CA-4 de infra/environments/dev/apim-mcp-prm.tf donde
# se declara una sola vez) -- RFC 9728 seccion 3.1 "Example with path component". La version del
# pionero (#558) declaraba un azurerm_api_management_api propio por servidor con path
# "api/.well-known/oauth-protected-resource": colisiona si un segundo servidor MCP (Comandos,
# #570/#571) intenta declarar el mismo path de nuevo, porque el path de gateway del PRM (sin el
# prefijo "api/" per-servidor) es UNICO por instancia APIM, no por servidor. Consistencia byte a byte (issue #558 decision #4, vigente):
# local.resource_uri y local.prm_url se arman DENTRO de este modulo a partir del MISMO
# var.gateway_url que alimenta Mcp__ResourceUri en el Function App (var.gateway_url = siempre
# module.api_management.gateway_url) -- el modulo nunca vuelve a reconstruir esos strings por
# fuera para no arriesgar un byte de diferencia entre <audience>, el `resource` del PRM y el
# Resource Indicator de WorkOS (manual, dashboard).

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
  description = "Resource group de la instancia APIM (los recursos hijos de este modulo viven ahi: backends, named value, api, policies)"
  type        = string
}

variable "gateway_url" {
  description = "URL publica del gateway APIM (module.api_management.gateway_url). El modulo arma local.resource_uri y local.prm_url UNICAMENTE a partir de este valor -- nunca lo reconstruyas en el caller ni pases un audience/prm_url ya armado, para no arriesgar un byte de diferencia entre <audience>, el campo 'resource' del PRM y el Resource Indicator del dashboard WorkOS (issue #558 decision #4)."
  type        = string
}

variable "api_name" {
  description = "Nombre interno (kebab-case) de la API APIM del protocolo de este servidor MCP (ej. 'mcp-consultas'). Prefijo de todos los recursos hijos de este modulo (backends, named value de la key, operation_id de la operacion PRM de este servidor en la API compartida)."
  type        = string
}

variable "display_name" {
  description = "Nombre legible del servidor MCP (aparece en el portal del desarrollador)"
  type        = string
}

variable "path" {
  description = "Path de gateway (kebab-case) de este servidor MCP, ej. 'mcp-consultas'. Se usa como path de la API del protocolo Y como sufijo de la operacion PRM compartida (GET /{path} sobre var.mcp_prm_api_name, RFC 9728 seccion 3.1 'Example with path component')."
  type        = string
}

variable "function_app_id" {
  description = "ID completo del Function App backend (azurerm_linux_function_app.id / module.function_app_*.id) -- requerido por azapi_resource_action para leer la system key mcp_extension via listkeys (issue #558 decision #6: azurerm_function_app_host_keys no la expone)."
  type        = string
}

# Revision infra-reviewer (#558): el hostname llega COMPUTADO desde el caller
# (module.function_app_*.default_hostname), no concatenado como "<name>.azurewebsites.net". El
# propio modulo function-app lo documenta asi ("usarlo en vez de concatenar name +
# .azurewebsites.net protege contra hostnames regionalizados"): Azure ya asigna hostnames
# regionalizados del tipo <name>-<hash>.<region>-01.azurewebsites.net a apps nuevas, y con el
# hostname adivinado el backend apuntaria a un host inexistente -- un fallo que ni
# `terraform validate` ni el plan detectan, solo el 404 en runtime (CA-4/CA-6). Desviacion #1
# frente al HCL de Mefisto (que concatena function_app_name + "azurewebsites.net"): reportada a
# Mefisto como draft (issue #575, reconciliacion CA-2 de harness#820).
variable "function_app_default_hostname" {
  description = "Hostname publico COMPUTADO del Function App backend (module.function_app_*.default_hostname, sin esquema). No lo construyas concatenando el nombre con 'azurewebsites.net': Azure asigna hostnames regionalizados."
  type        = string
}

variable "authorization_server_url" {
  description = "Dominio AuthKit del entorno contra el que se arma el issuer/openid-config de esta politica (issue #561, misma causa raiz que #560 en el app-side: los tokens del flujo MCP/Connect se emiten con issuer = dominio AuthKit, NO 'https://api.workos.com/user_management/{client_id}' -- ese es el issuer de LOGIN, MEF-ADR-0032 seccion 6). Verificado en vivo 2026-09-01: '/.well-known/openid-configuration' de este dominio responde 200 con issuer = el dominio y jwks en '/oauth2/jwks'. Sin default: el caller lo fija explicitamente al dominio AuthKit del entorno (ej. 'https://marvelous-polaroid-97-staging.authkit.app' en dev), sin barra final."
  type        = string
}

variable "mcp_prm_api_name" {
  description = "Nombre de la API APIM compartida del PRM (azurerm_api_management_api.mcp_prm.name, declarada UNA sola vez en infra/environments/dev/apim-mcp-prm.tf, forma de Mefisto Paso 3c.2). Este modulo agrega sobre ella UNA operacion GET /{var.path} propia de este servidor -- nunca declara su propia API de PRM (issue #575: eso colisionaba con un segundo servidor, ver el comentario de cabecera)."
  type        = string
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
  # comentario de azurerm_api_management_backend.protocol). Desviacion #3 frente al HCL de
  # Mefisto (issue #575): Mefisto pone el path del servidor en la base-url del backend sin
  # rewrite-uri -- con el wildcard "/*" de la operacion, APIM concatena cualquier sufijo que el
  # cliente llame de mas (ej. una barra final) y ese cliente recibe 404 en vez de llegar al mismo
  # endpoint del protocolo.
  function_app_base_url = "https://${var.function_app_default_hostname}"

  # Unica fuente de verdad de las tres piezas que MEF-ADR-0032/issue #558 (decision #4) exige que
  # coincidan byte a byte: <audience> de la politica del protocolo, campo "resource" del
  # documento PRM y Resource Indicator del dashboard WorkOS (manual, checklist del PR).
  # trimsuffix defiende contra que var.gateway_url venga con o sin "/" final. Formula del issue
  # #575 (forma canonica de Mefisto Paso 2b, PRM compartido bajo "{prm}/{path}", RFC 9728 seccion
  # 3.1) con UNA desviacion: el segmento del PRM va SIN punto inicial ("well-known/..."), porque
  # APIM rechaza un path de API que empiece con punto (400 ValidationError, CA-4 de #575
  # verificado en rojo en el apply -- ver infra/environments/dev/apim-mcp-prm.tf). Debe coincidir
  # byte a byte con el `path` de azurerm_api_management_api.mcp_prm: es la URL que viaja en el
  # WWW-Authenticate resource_metadata del 401 (RFC 9728 seccion 5.1) y el output prm_url.
  gateway_url_trimmed = trimsuffix(var.gateway_url, "/")
  resource_uri        = "${local.gateway_url_trimmed}/${var.path}"
  prm_url             = "${local.gateway_url_trimmed}/well-known/oauth-protected-resource/${var.path}"

  # trimsuffix defiende contra que el caller pase el dominio con "/" final (mismo patron que
  # local.gateway_url_trimmed): el issuer debe coincidir byte a byte con el que emite el
  # discovery doc en vivo (RFC 8414 / MEF-ADR-0032 seccion 8), sin barra.
  workos_issuer            = trimsuffix(var.authorization_server_url, "/")
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
# modulo, esta key NUNCA se expone en un output de este modulo. Desviacion #4 frente al HCL de
# Mefisto (issue #575): Mefisto usa un `data` source para esta key, evaluado en PLAN -- la key
# mcp_extension solo existe DESPUES del primer deploy del codigo del Function App, asi que un
# `data` fallaria en el primer plan/apply de un servidor nuevo. `resource` (azapi_resource_action)
# se evalua en APPLY, cuando el codigo ya esta desplegado.
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
  name                = "${var.api_name}-mcp-extension-key"
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  display_name        = "${var.api_name}-mcp-extension-key"
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
  name                = "${var.api_name}-protocol-backend"
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
  name                  = var.api_name
  resource_group_name   = var.resource_group_name
  api_management_name   = var.api_management_name
  revision              = "1"
  display_name          = var.display_name
  path                  = var.path
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
# global. on-error emite el WWW-Authenticate con resource_metadata apuntando al PRM compartido
# (decision #2, issue #575 lo repunta a local.prm_url) sin condicionar por status code -- esta API
# solo hace dos cosas (validar el JWT y reenviar al backend del protocolo), asi que cualquier
# error que llegue a on-error es, en la practica, un rechazo de validate-jwt (que ya fijo el 401
# via failed-validation-httpcode).
# Orden de hijos de <validate-jwt> (XSD de APIM, learn.microsoft.com/azure/api-management/
# validate-jwt-policy: "set the policy's elements and child elements in the order provided"):
# openid-config -> issuer-signing-keys -> decryption-keys -> audiences -> issuers -> required-claims.
# <audiences> va ANTES de <issuers>. Desviacion #2 frente al HCL de Mefisto (issue #575): Mefisto
# lo tiene invertido (issuers antes que audiences) y ese orden hizo fallar el apply con un
# 400 ValidationError sin detalle (run 33566692118) que ni validate ni plan detectan.
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
        <audience>${local.resource_uri}</audience>
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
      <value>Bearer error="invalid_token", resource_metadata="${local.prm_url}"</value>
    </set-header>
  </on-error>
</policies>
XML
}

# ---- Operacion PRM de ESTE servidor sobre la API COMPARTIDA var.mcp_prm_api_name ----
#
# Forma de Mefisto (issue #575): el PRM deja de ser una API propia por servidor (version #558) y
# pasa a ser una operacion mas ("GET /{var.path}") de la API compartida
# "well-known/oauth-protected-resource" (declarada una sola vez en apim-mcp-prm.tf; sin punto
# inicial, ver su comentario CA-4) -- RFC 9728 seccion 3.1 "Example with path component". Sin validate-jwt (spec de autorizacion MCP: un
# cliente sin token todavia debe poder leer este documento); la API compartida ya es anonima
# (azurerm_api_management_api.mcp_prm, subscription_required = false) y su politica de API NO
# lleva <base/> (reemplaza la global, igual que la politica del protocolo de arriba) -- la
# operacion hereda ESA politica anonima via <base/>, nunca la global de LOGIN.
#
# Backend propio (no el mismo que el protocolo): mismo patron que #558 (base RAIZ + rewrite-uri
# explicito). El Function endpoint YA es AuthorizationLevel.Anonymous del lado del worker
# (FunctionEndpoint.cs) -- este backend tampoco inyecta la system key, no hace falta.
resource "azurerm_api_management_backend" "prm" {
  name                = "${var.api_name}-prm-backend"
  resource_group_name = var.resource_group_name
  api_management_name = var.api_management_name
  protocol            = "http"
  url                 = local.function_app_base_url
}

resource "azurerm_api_management_api_operation" "prm" {
  operation_id        = "get-prm-${var.api_name}"
  api_name            = var.mcp_prm_api_name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  display_name        = "GET PRM ${var.display_name}"
  method              = "GET"
  url_template        = "/${var.path}"
}

# El path fijo del rewrite ("/api/.well-known/oauth-protected-resource") lo impone el CODIGO
# (UriMetadataRecursoProtegido deriva esa ruta ABSOLUTA sobre el host de Mcp__ResourceUri) -- no
# cambiar sin cambiar tambien FunctionEndpoint.Ruta en el servidor MCP correspondiente. NO
# depende de var.path: es la misma ruta interna en TODOS los servidores MCP del BC, cada uno la
# sirve en su propio Function App backend.
resource "azurerm_api_management_api_operation_policy" "prm" {
  api_name            = var.mcp_prm_api_name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  operation_id        = azurerm_api_management_api_operation.prm.operation_id

  xml_content = <<XML
<policies>
  <inbound>
    <base />
    <rewrite-uri template="/api/.well-known/oauth-protected-resource" copy-unmatched-params="false" />
    <set-backend-service backend-id="${azurerm_api_management_backend.prm.name}" />
  </inbound>
  <backend>
    <base />
  </backend>
  <outbound>
    <base />
  </outbound>
  <on-error>
    <base />
  </on-error>
</policies>
XML
}

# Ningun output expone la system key mcp_extension (MEF-ADR-0025): vive solo en el named value
# secreto y en el state, nunca en una salida legible.

output "resource_uri" {
  description = "URL publica de APIM del endpoint del protocolo MCP de este servidor (module.api_management.gateway_url + '/' + var.path). Debe coincidir byte a byte con el Resource Indicator del dashboard WorkOS y con el campo 'resource' del PRM (output prm_url) -- el caller lo usa para fijar Mcp__ResourceUri en el Function App."
  value       = local.resource_uri
}

output "prm_url" {
  description = "URL publica de APIM del documento PRM (RFC 9728) de este servidor, alcanzable anonimamente en el PRM compartido. Su campo 'resource' debe coincidir byte a byte con resource_uri."
  value       = local.prm_url
}

output "protocol_api_id" {
  description = "ID de la API APIM del endpoint del protocolo MCP"
  value       = azurerm_api_management_api.protocol.id
}

output "protocol_api_name" {
  description = "Nombre de la API APIM del endpoint del protocolo MCP"
  value       = azurerm_api_management_api.protocol.name
}
