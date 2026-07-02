variable "name" {
  description = "Nombre del Key Vault (3-24 chars, alfanumerico y guiones, debe empezar con letra)"
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

variable "tenant_id" {
  description = "Tenant ID de Azure AD (usar data.azurerm_client_config.current.tenant_id)"
  type        = string
}

variable "sku_name" {
  description = "SKU del Key Vault: standard o premium"
  type        = string
  default     = "standard"
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

# Almacen general de secretos del BC (ADR-0025 decision #5): custodia cadenas
# de conexion de Service Bus, password de PostgreSQL y connection string de
# Application Insights. RBAC habilitado (ADR-0024 decision #6): nunca access
# policies. Este modulo NO crea secretos: el valor lo coloca un admin de forma
# administrativa (`az keyvault secret set`), nunca Terraform.
resource "azurerm_key_vault" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  tenant_id           = var.tenant_id
  sku_name            = var.sku_name
  # `enable_rbac_authorization` (nombre historico del ADR-0021 del harness)
  # esta deprecado desde azurerm ~> 4.x en favor de `rbac_authorization_enabled`
  # y sera removido en v5.0 del provider (fuente: terraform-provider-azurerm,
  # website/docs/r/key_vault.html.markdown, seccion "Arguments Reference").
  rbac_authorization_enabled = true
  soft_delete_retention_days = 7
  tags                       = var.tags

  lifecycle {
    prevent_destroy = true
  }
}

output "id" {
  value = azurerm_key_vault.this.id
}

output "name" {
  value = azurerm_key_vault.this.name
}

output "uri" {
  description = "URI base del Key Vault (https://<vault>.vault.azure.net/). Usar para construir referencias @Microsoft.KeyVault(SecretUri=<uri>secrets/<secretName>) versionless"
  value       = azurerm_key_vault.this.vault_uri
}
