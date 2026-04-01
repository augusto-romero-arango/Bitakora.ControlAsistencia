output "resource_group_name" {
  description = "Nombre del resource group"
  value       = module.resource_group.name
}

output "function_app_names" {
  description = "Nombres de las Function Apps por dominio"
  value       = { for k, v in module.function_apps : k => v.name }
}

output "service_bus_name" {
  description = "Nombre del Service Bus namespace"
  value       = module.service_bus.name
}
