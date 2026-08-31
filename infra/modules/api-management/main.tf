# Modulo APIM (MEF-ADR-0032, issue #335): instancia Consumption + politica GLOBAL (cors +
# validate-jwt + propagacion de identidad claim -> header). Fuente de verdad: Cosmos.ControlPlane
# (ADR-0027 del consumidor, PRs #96-#100/#103/#104). Catalogo de trampas B1-B11 verificado
# contra Microsoft Learn (validate-jwt, cors, set-edit-policies) -- ver docs/adr/mef-adr-0032-...
# de Mefisto para las citas completas. Cada nota de trampa es un comentario HCL: el schema de
# validate-jwt NO admite comentarios XML interpuestos entre openid-config/issuers/required-claims
# (B6), asi que ninguna nota va dentro de xml_content.

variable "name" {
  description = "Nombre de la instancia APIM, YA compuesto por el caller con el patron CAF {app}-{env}-{region}-{seq} (MEF-ADR-0045, B9: '<name>.azure-api.net' es unico en TODO Azure -- mismo patron que postgresql/service-bus/key-vault en infra-base-scaffolder, sin sufijo random)"
  type        = string
}

variable "resource_group_name" {
  description = "Nombre del resource group"
  type        = string
}

variable "location" {
  description = "Region de Azure"
  type        = string
}

variable "publisher_name" {
  description = "Nombre del publisher (requerido por azurerm_api_management, aparece en el portal del desarrollador)"
  type        = string
}

variable "publisher_email" {
  description = "Email del publisher (requerido por azurerm_api_management)"
  type        = string
}

variable "cors_allowed_origins" {
  description = "Origenes permitidos del SPA para el preflight CORS. B3: sin <cors> ANTES de <validate-jwt> en la politica global, el preflight OPTIONS (sin header Authorization) lo tumba validate-jwt con 401, o el navegador ve 404 y bloquea la llamada real -- Microsoft Learn confirma que 'only the cors policy is evaluated on the OPTIONS request during preflight'."
  type        = list(string)

  validation {
    condition     = length(var.cors_allowed_origins) > 0
    error_message = "cors_allowed_origins no puede venir vacio (B3): sin al menos un origen, el preflight del SPA nunca matchea."
  }
}

variable "workos_client_id" {
  description = "Client ID del proyecto WorkOS AuthKit de LOGIN (MEF-ADR-0032 seccion 6 -- no confundir con el API key del proyecto de negocio, que vive en la Function App consumidora). No es secreto. B4: WorkOS AuthKit no emite el claim 'aud'; este valor se usa como required-claim sobre 'client_id' en vez de <audiences>. B5: tambien construye el discovery endpoint client-specific -- reverificar el 'issuer'/'jwks_uri' contra el discovery doc en vivo del proyecto concreto antes de aplicar (docs/adr/mef-adr-0032, seccion 8: 'NO VERIFICADO en documentacion publica')."
  type        = string
}

variable "claim_user_id" {
  description = "Nombre EXACTO del claim del JWT mapeado a X-User-Id. B10: NO se adivina -- se confirma decodificando un token real del proyecto WorkOS concreto ('email' fue el nombre adivinado en ControlPlane y produjo un header vacio via GetValueOrDefault). Default = mapeo confirmado en Cosmos.ControlPlane; reverificar por consumidor."
  type        = string
  default     = "user_email"
}

variable "claim_tenant_id" {
  description = "Nombre EXACTO del claim del JWT mapeado a X-Tenant-Id. B10: NO se adivina -- se confirma decodificando un token real del proyecto WorkOS concreto. Default = mapeo confirmado en Cosmos.ControlPlane; reverificar por consumidor."
  type        = string
  default     = "tenant_id"
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

locals {
  # B5: issuer client-specific (NUNCA 'https://api.workos.com' a secas) -- Cosmos.ControlPlane
  # confirmo esta variante leyendo el discovery doc en vivo:
  # GET https://api.workos.com/user_management/{client_id}/.well-known/openid-configuration
  # Reverificar el campo 'issuer' del discovery doc real del proyecto WorkOS concreto antes
  # de dar por buena esta formula en un consumidor nuevo (Paso 0.3 de este agente).
  workos_openid_config_url = "https://api.workos.com/user_management/${var.workos_client_id}/.well-known/openid-configuration"
  workos_issuer            = "https://api.workos.com/user_management/${var.workos_client_id}"
}

# B9: tier Consumption (sku_name = "Consumption_0", confirmado contra el provider azurerm --
# "Consumption SKU capacity should be 0"). validate-jwt esta disponible en TODOS los tiers de
# APIM incluido Consumption; la contrapartida es sin VNet, sin rate-limit-by-key, sin Log
# Analytics de requests (si App Insights). identity SystemAssigned queda reservada para wiring
# futuro (p.ej. named values respaldados por Key Vault); esta version custodia la host key
# directamente como named value secreto (modulo apim-function-api, B8).
resource "azurerm_api_management" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  publisher_name      = var.publisher_name
  publisher_email     = var.publisher_email
  sku_name            = "Consumption_0"

  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

# Politica GLOBAL (B1: scope SIN padre -- Microsoft Learn: "a globally scoped policy has no
# parent scope, and using the base element in it has no effect"; ControlPlane observo ademas
# un 400 ValidationError al intentarlo via azurerm, mas estricto que "sin efecto"). Por eso
# esta politica NUNCA lleva <base/> en ninguna seccion -- a diferencia de la politica por-API
# del modulo apim-function-api, que SI hereda de esta.
#
# Trampas que viven DENTRO del xml_content de abajo y por eso se documentan aca como comentario
# HCL (B6 prohibe comentarios XML <!-- --> interpuestos entre los hijos de validate-jwt):
#   B2: <backend> DEBE contener <forward-request /> -- si queda vacio, APIM responde
#       200/Content-Length: 0 y NUNCA reenvia al backend (el bug mas traicionero del catalogo:
#       "acepta y no hace nada", confirmado por ausencia total de requests en App Insights).
#   B3: <cors> es el PRIMER hijo de <inbound>, ANTES de <validate-jwt> -- el preflight OPTIONS no
#       trae header Authorization; si validate-jwt lo intercepta primero lo tumba con 401.
#   B3 (continuacion, issue #608): <allowed-methods> incluye QUERY -- RFC 10008 seccion 4 es
#       explicito en que QUERY no es CORS-safelisted, asi que un SPA que lo use siempre dispara
#       preflight. Se lista por enumeracion EXPLICITA, nunca "*": la doc oficial de la politica
#       cors confirma que '* indicates all methods', pero este marco descarta ese wildcard a
#       proposito (postura deny-by-default; doctrina en MEF-ADR-0032 seccion 3 B3, verbo en
#       MEF-ADR-0042).
#   B4: WorkOS AuthKit no emite el claim `aud` -> nada de <audiences>; la "audiencia" se valida
#       con <required-claims> sobre client_id.
#   B6: orden estricto openid-config -> issuers -> required-claims dentro de <validate-jwt>.
#   B10: los <set-header> de identidad van DESPUES de </validate-jwt> (usan context.Variables["jwt"],
#        capturado por output-token-variable-name="jwt") y SIEMPRE con exists-action="override"
#        (anti-spoofing: sin override, un cliente que manda su propio X-User-Id/X-Tenant-Id lo
#        cuela intacto hasta el backend).
#
# B7 (diagnostico): si `terraform apply` falla aca con un 400 ValidationError generico/truncado
# ("One or more fields contain incorrect values:" sin decir que campo), reproduce el PUT de la
# politica directo con `az rest --method put --url ".../policies/policy?api-version=2022-08-01"
# --body @body.json` -- la respuesta de az SI trae error.details[].target/.message con el
# elemento exacto que falla.
resource "azurerm_api_management_policy" "global" {
  api_management_id = azurerm_api_management.this.id

  xml_content = <<XML
<policies>
  <inbound>
    <cors allow-credentials="false">
      <allowed-origins>
%{for origin in var.cors_allowed_origins~}
        <origin>${origin}</origin>
%{endfor~}
      </allowed-origins>
      <allowed-methods preflight-result-max-age="300">
        <method>GET</method>
        <method>POST</method>
        <method>PUT</method>
        <method>DELETE</method>
        <method>OPTIONS</method>
        <method>QUERY</method>
      </allowed-methods>
      <allowed-headers>
        <header>Authorization</header>
        <header>Content-Type</header>
      </allowed-headers>
    </cors>
    <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Unauthorized." output-token-variable-name="jwt">
      <openid-config url="${local.workos_openid_config_url}" />
      <issuers>
        <issuer>${local.workos_issuer}</issuer>
      </issuers>
      <required-claims>
        <claim name="client_id" match="all">
          <value>${var.workos_client_id}</value>
        </claim>
      </required-claims>
    </validate-jwt>
    <set-header name="X-User-Id" exists-action="override">
      <value>@(((Jwt)context.Variables["jwt"]).Claims.GetValueOrDefault("${var.claim_user_id}", ""))</value>
    </set-header>
    <set-header name="X-Tenant-Id" exists-action="override">
      <value>@(((Jwt)context.Variables["jwt"]).Claims.GetValueOrDefault("${var.claim_tenant_id}", ""))</value>
    </set-header>
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

output "id" {
  value = azurerm_api_management.this.id
}

output "name" {
  value = azurerm_api_management.this.name
}

output "gateway_url" {
  description = "URL publica del gateway ('<name>.azure-api.net') -- unico front door del BC (MEF-ADR-0032). El SPA/cliente llama aqui, nunca directo a las Function Apps."
  value       = azurerm_api_management.this.gateway_url
}

output "principal_id" {
  description = "Principal ID de la managed identity SystemAssigned"
  value       = azurerm_api_management.this.identity[0].principal_id
}
