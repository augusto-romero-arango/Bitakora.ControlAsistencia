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
