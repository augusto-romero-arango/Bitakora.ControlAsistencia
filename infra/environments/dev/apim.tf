# Wiring del gateway APIM (MEF-ADR-0032, issue #335): front door unico que valida el JWT de
# WorkOS AuthKit en el borde y reenvia a las Function Apps del BC. Se instancia UNA sola vez
# por entorno (a diferencia de apim-dominio-{kebab}.tf, que se agrega uno por dominio -- ver
# agents/apim-gateway-scaffolder.md). NO regeneres este archivo si ya existe (CA-6: aditivo --
# agregar un dominio nuevo nunca toca este archivo).
#
# Nombre de la instancia (MEF-ADR-0045): "apim-${local.prefix}" = "apim-controlasistencias-dev".
# Este entorno se scaffoldeo ANTES de que infra-base-scaffolder adoptara el patron CAF completo
# {app}-{env}-{region}-{seq} -- local.prefix aqui es solo "${var.project}-${var.environment}"
# (variables.tf), sin {region}-{seq}. El resultado, "apim-controlasistencias-dev", es el nombre
# CORRECTO para este entorno: coincide con el patron ya vigente en el resource group
# (rg-controlasistencias-dev), Service Bus (sb-controlasistencias-dev) y demas recursos de
# main.tf. NO se le inyecta {region}-{seq} a local.prefix para "completar" el patron generico:
# eso renombraria de golpe todos esos recursos ya desplegados (destroy+recreate, MEF-ADR-0045
# seccion 3 -- ver tambien el guard analogo en agents/infra-base-scaffolder.md).
#
# publisher_email: la plantilla generica de este agente asume una variable de entorno
# `var.alert_email` (provista por infra-base-scaffolder). ESTE proyecto no la tiene: el email
# de alertas de costo vive como default fijo DENTRO del modulo monitoring
# (infra/modules/monitoring/main.tf, variable alert_action_group_email = "augromara@gmail.com"),
# nunca expuesto como variable de root module ni como output. Divergencia documentada (CA-5):
# se declara aqui una variable propia `apim_publisher_email` con el MISMO valor por defecto,
# en vez de inventar un `var.alert_email` que no existe en el resto del entorno.

variable "workos_client_id" {
  description = "Client ID del proyecto WorkOS AuthKit de LOGIN (MEF-ADR-0032 seccion 6 -- NO el API key del proyecto de negocio, que vive en la Function App consumidora). Publico, no secreto."
  type        = string
}

variable "cors_allowed_origins" {
  description = "Origenes permitidos del SPA para el preflight CORS (B3)"
  type        = list(string)
}

variable "apim_claim_user_id" {
  description = "Nombre EXACTO del claim del JWT mapeado a X-User-Id (B10 -- confirmar decodificando un token real antes de aceptar el default)"
  type        = string
  default     = "user_email"
}

variable "apim_claim_tenant_id" {
  description = "Nombre EXACTO del claim del JWT mapeado a X-Tenant-Id (B10 -- confirmar decodificando un token real antes de aceptar el default)"
  type        = string
  default     = "tenant_id"
}

variable "apim_publisher_email" {
  description = "Email del publisher de la instancia APIM (requerido por azurerm_api_management). Este proyecto no tiene var.alert_email de nivel raiz (ver nota arriba); default = mismo email que ya recibe las alertas de costo de Application Insights (infra/modules/monitoring, alert_action_group_email)."
  type        = string
  default     = "augromara@gmail.com"
}

# B9: '<name>.azure-api.net' es unico en TODO Azure. La unicidad la da la composicion
# {app}-{env}-{region}-{seq} de local.prefix -- sin sufijo random, el nombre es predecible antes
# de aplicar (MEF-ADR-0045 seccion 2; mismo patron que postgresql/service-bus/key-vault en
# infra-base-scaffolder.md Paso 2.2/2.3). Ante una colision real en Azure, el fallback es
# incrementar resourceSequence en harness.config.json, nunca reintroducir un random_string.
module "api_management" {
  source              = "../../modules/api-management"
  name                = "apim-${local.prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  publisher_name      = var.project
  publisher_email     = var.apim_publisher_email

  cors_allowed_origins = var.cors_allowed_origins
  # ADVERTENCIA B5 (MEF-ADR-0032 seccion 8, verificacion Paso 0.3 de este agente, 2026-08-31):
  # el discovery doc en vivo de ESTE client_id (GitHub variable WORKOS_CLIENT_ID =
  # "client_01M1CM85AYQEW4H7YRDNZN3AA6") devolvio issuer/jwks_uri referenciando un client_id
  # DISTINTO: "client_01M1CKPECJ5DBRMS3ZVFRQW8GW". Ver el detalle completo (respuesta HTTP 200
  # completa, y contraste contra un client_id invalido que SI devuelve 404 "Application not
  # found") en el reporte final de esta corrida del agente. NO VERIFICADO -- reconfirmar con el
  # dashboard de WorkOS cual client_id es realmente el correcto ANTES de aplicar: con la
  # discrepancia sin resolver, la politica <issuers> de la global policy quedaria construida
  # sobre un issuer que ningun JWT real de este proyecto WorkOS emite, y validate-jwt
  # rechazaria con 401 absolutamente todos los logins.
  workos_client_id = var.workos_client_id
  claim_user_id    = var.apim_claim_user_id
  claim_tenant_id  = var.apim_claim_tenant_id

  tags = local.tags
}

output "apim_gateway_url" {
  description = "URL publica del gateway ('<name>.azure-api.net') -- unico front door del BC (MEF-ADR-0032). El SPA/cliente llama aqui, nunca directo a las Function Apps."
  value       = module.api_management.gateway_url
}
