output "resource_group_name" {
  description = "Nombre del resource group"
  value       = module.resource_group.name
}

output "service_bus_name" {
  description = "Nombre del Service Bus namespace"
  value       = module.service_bus.name
}

output "postgresql_fqdn" {
  description = "FQDN del servidor PostgreSQL"
  value       = module.postgresql.server_fqdn
}

output "key_vault_name" {
  description = "Nombre del Key Vault del BC (almacen general de secretos, ADR-0025 decision #5)"
  value       = module.key_vault.name
}

output "key_vault_uri" {
  description = "URI base del Key Vault. Usar para construir referencias @Microsoft.KeyVault(SecretUri=<uri>secrets/<secretName>) versionless"
  value       = module.key_vault.uri
}

# Datos crudos para la siembra administrativa post-apply (ADR-0025 decision #6):
# un admin coloca los secretos con `az keyvault secret set`, nunca Terraform.

output "postgresql_database_name" {
  description = "Nombre de la base de datos PostgreSQL, para armar el secreto marten-connection"
  value       = module.postgresql.database_name
}

output "postgresql_administrator_login" {
  description = "Usuario administrador de PostgreSQL, para armar el secreto marten-connection"
  value       = module.postgresql.administrator_login
}

output "service_bus_connection_string" {
  description = "Cadena de conexion del Service Bus, para sembrar el secreto service-bus-connection"
  value       = module.service_bus.default_primary_connection_string
  sensitive   = true
}

output "app_insights_connection_string" {
  description = "Connection string de Application Insights, para sembrar el secreto app-insights-connection"
  value       = module.monitoring.connection_string
  sensitive   = true
}

# Worker de proyecciones (opt-in, MEF-ADR-0034 seccion 8; issue #234)

output "container_registry_login_server" {
  description = "Hostname del Container Registry del worker de proyecciones. Lo consume el pipeline de CI de imagen (fuera de alcance de este issue) para saber a donde empujar Bitakora.ControlAsistencia.Projections"
  value       = module.container_registry.login_server
}

output "container_app_name" {
  description = "Nombre del Container App del worker de proyecciones. Lo consume el pipeline de CI de imagen para actualizar la revision (ej. az containerapp update --image ...) tras publicar una imagen nueva"
  value       = module.container_app.name
}
