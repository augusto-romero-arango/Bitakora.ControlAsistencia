# Modulo apim-function-api (MEF-ADR-0032, issue #335): una API por dominio detras del gateway
# APIM del modulo api-management. Trampas B7-B9 y B11 aplicadas aqui (B1-B6/B10 viven en la
# politica GLOBAL del modulo api-management). A diferencia de esa politica global, esta SI usa
# <base/>: hereda cors + validate-jwt + propagacion de identidad + forward-request de la global.

variable "api_management_name" {
  description = "Nombre de la instancia APIM (module.api_management.name del modulo api-management)"
  type        = string
}

variable "resource_group_name" {
  description = "Resource group de la instancia APIM (los recursos hijos de esta API viven ahi: backend, named value, api, policy)"
  type        = string
}

variable "api_name" {
  description = "Identificador de la API (unico dentro de la instancia APIM), tipicamente el dominio en kebab-case"
  type        = string
}

variable "display_name" {
  description = "Nombre legible de la API (aparece en el portal del desarrollador)"
  type        = string
}

variable "path" {
  description = "Segmento de URL de la API bajo el gateway (https://<apim>.azure-api.net/<path>/...)"
  type        = string
}

variable "function_app_name" {
  description = "Nombre de la Function App backend (module.function_app_{dominio}.name del domain-scaffolder)"
  type        = string
}

variable "function_app_resource_group_name" {
  description = "Resource group de la Function App backend (puede diferir del resource_group_name de la API si el BC separa RGs; en este marco tipicamente coinciden -- domain-scaffolder pone todo en module.resource_group)"
  type        = string
}

variable "function_app_hostname_suffix" {
  description = "Sufijo del hostname publico por defecto de la Function App (B8). 'azurewebsites.net' en Azure publico global; ajustar en nubes soberanas (p.ej. Azure Government)."
  type        = string
  default     = "azurewebsites.net"
}

variable "operation_methods" {
  description = "Verbos HTTP wildcard a exponer en esta API (B11 de MEF-ADR-0032, issue #610: opcion (b) -- operaciones wildcard por verbo, no una operacion explicita por endpoint del dominio). Default = los tres verbos vigentes del marco: comandos POST y queries GET (MEF-ADR-0006), mas queries estructuradas QUERY (RFC 10008/MEF-ADR-0042, issue #608) -- el REST API reference de ApiOperation confirma que 'method' es 'A Valid HTTP Operation Method... but not limited by only [GET, PUT, POST]', y el schema del provider azurerm repite lo mismo para este recurso ('The HTTP Method used for this API Management Operation, like GET, DELETE, PUT or POST - but not limited to these values', registry azurerm 5.0.1): no hay enum que `terraform validate` pueda rechazar. NUNCA agregues OPTIONS a esta lista: la referencia de la politica cors es explicita en que 'if a request matches an operation with an OPTIONS method defined in the API, preflight request processing logic associated with the cors policy will not be executed' -- declarar OPTIONS aqui desactiva el manejo automatico del preflight y reintroduce B3 (el navegador vuelve a quedarse sin respuesta de CORS)."
  type        = list(string)
  default     = ["GET", "POST", "QUERY"]
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

locals {
  function_app_default_hostname = "${var.function_app_name}.${var.function_app_hostname_suffix}"
}

# B8: data.azurerm_function_app_host_keys expone default_function_key (verificado contra el
# provider azurerm; la propia doc del data source advierte que TODOS sus atributos, incluido
# default_function_key, quedan en texto plano en el state -- por eso este modulo nunca expone
# la host key como output, y el remote state del entorno debe tratarse como secreto, MEF-ADR-0025).
data "azurerm_function_app_host_keys" "this" {
  name                = var.function_app_name
  resource_group_name = var.function_app_resource_group_name
}

# B8: la host key se custodia como named value SECRETO -- nunca como valor literal en el HCL
# ni en un output legible en claro (MEF-ADR-0025). secret = true no vuelve sensible el
# atributo en el STATE de Terraform (queda en texto plano ahi tambien; solo se cifra dentro
# de APIM) -- confirmado contra la doc del provider azurerm.
resource "azurerm_api_management_named_value" "function_key" {
  name                = "${var.api_name}-func-key"
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  display_name        = "${var.api_name}-func-key"
  value               = data.azurerm_function_app_host_keys.this.default_function_key
  secret              = true
}

# B8: 'header' es map(string), NO un bloque; el named value se referencia con {{...}}.
resource "azurerm_api_management_backend" "this" {
  name                = "${var.api_name}-backend"
  resource_group_name = var.resource_group_name
  api_management_name = var.api_management_name
  protocol            = "http"
  url                 = "https://${local.function_app_default_hostname}/api"

  credentials {
    header = {
      "x-functions-key" = "{{${azurerm_api_management_named_value.function_key.name}}}"
    }
  }
}

# B9: subscription_required = false -- la puerta de acceso es el JWT que valida la politica
# global, no una subscription key de APIM (el default del recurso es 'true'; hay que
# desactivarlo explicito).
resource "azurerm_api_management_api" "this" {
  name                  = var.api_name
  resource_group_name   = var.resource_group_name
  api_management_name   = var.api_management_name
  revision              = "1"
  display_name          = var.display_name
  path                  = var.path
  protocols             = ["https"]
  subscription_required = false
}

# B11 (MEF-ADR-0032, issue #610): sin NINGUNA azurerm_api_management_api_operation, APIM responde
# 404 a TODO el trafico -- incluso con JWT ya validado por la politica global -- porque por
# defecto ninguna operacion queda expuesta hasta declararla explicitamente (Microsoft Learn,
# "Manually add an API": "By default, when you add an API, even if it's connected to a backend
# service, API Management won't expose any operations until you allow them"; "If you call an
# operation that's exposed through the backend but not through API Management, you get a 404
# error"). Opcion (b) (issue #610): una operacion WILDCARD por verbo (`url_template = "/*"`,
# "Add and test a wildcard operation", Microsoft Learn), no una operacion explicita por endpoint
# del dominio (fiel a Cosmos.ControlPlane, gateway.tf, pero rompe la aditividad CA-6: cada Function
# nueva del dominio consumidor exigiria tocar esta infra). La wildcard preserva CA-6 intacta: el
# `<forward-request/>` de B2 ya hace el passthrough completo, esta operacion solo la habilita.
#
# Trade-off aceptado y documentado, no una omision: la guia de mitigacion OWASP API5:2023 de
# Microsoft recomienda EXPLICITAMENTE no usar operaciones wildcard ("Don't define wildcard API
# operations (that is, 'catch-all' APIs with * as the path). Ensure that API Management only
# serves requests for explicitly defined endpoints, and requests to undefined endpoints are
# rejected", mitigate-owasp-api-threats#broken-function-level-authorization). Este marco se
# aparta de esa recomendacion a proposito: el limite de seguridad real del patron no es el
# catalogo de operaciones de APIM, es la politica validate-jwt GLOBAL (B1-B6/B10 de este mismo
# ADR), que se evalua para TODO match de ruta sea la operacion wildcard o explicita -- una
# operacion wildcard no abre ninguna superficie que el JWT no cierre. Un consumidor que priorice
# gobernanza per-endpoint sobre aditividad puede reemplazar este recurso por una operacion
# explicita por endpoint (opcion (a), descartada aqui como default).
#
# OPTIONS queda FUERA de var.operation_methods a proposito, y no es un olvido: la referencia de
# la politica cors dice que "if a request matches an operation with an OPTIONS method defined in
# the API, preflight request processing logic associated with the cors policy will not be
# executed". Declarar una operacion OPTIONS aqui desactivaria el manejo automatico del preflight
# que la politica global hace por B3 -- el gateway dejaria de responder el preflight del SPA.
resource "azurerm_api_management_api_operation" "wildcard" {
  for_each = toset(var.operation_methods)

  operation_id        = "${lower(each.value)}-wildcard"
  api_name            = azurerm_api_management_api.this.name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name
  display_name        = "${each.value} wildcard"
  method              = each.value
  url_template        = "/*"
}

resource "azurerm_api_management_api_policy" "this" {
  api_name            = azurerm_api_management_api.this.name
  api_management_name = var.api_management_name
  resource_group_name = var.resource_group_name

  xml_content = <<XML
<policies>
  <inbound>
    <base />
    <set-backend-service backend-id="${azurerm_api_management_backend.this.name}" />
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

output "id" {
  value = azurerm_api_management_api.this.id
}

output "name" {
  value = azurerm_api_management_api.this.name
}

output "backend_name" {
  value = azurerm_api_management_backend.this.name
}
