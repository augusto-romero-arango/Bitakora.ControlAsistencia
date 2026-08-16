variable "name" {
  description = "Nombre del service plan"
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

variable "sku_name" {
  description = "SKU del plan: B1=Basic (minimo para .NET 10), EP1=Elastic Premium"
  type        = string
  default     = "B1"
}

variable "os_type" {
  description = "Sistema operativo del plan. ForceNew en azurerm_service_plan: cambiarlo destruye el plan."
  type        = string
  default     = "Linux"
}

variable "worker_count" {
  description = "Numero de instancias (workers) del plan. DurabilityMode.Solo de Wolverine (MEF-ADR-0020) exige un unico nodo: no usar este input para escalar out."
  type        = number
  default     = 1
}

variable "always_on" {
  description = <<-EOT
    Si el/los Function App(s) de este plan deben correr con Always On. El
    recurso azurerm_service_plan no tiene este argumento (vive en el
    site_config de azurerm_linux_function_app): este modulo solo lo recibe y
    lo reexpone via output para que el modulo function-app lo consuma,
    centralizando en un unico lugar los parametros de hosting por dominio
    (contrato de inputs de MEF-ADR-0020).
  EOT
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

resource "azurerm_service_plan" "this" {
  name                = var.name
  resource_group_name = var.resource_group_name
  location            = var.location
  os_type             = var.os_type
  sku_name            = var.sku_name
  worker_count        = var.worker_count
  tags                = var.tags
}

output "id" {
  value = azurerm_service_plan.this.id
}

output "always_on" {
  description = "Pass-through de var.always_on para que el modulo function-app lo consuma en su site_config."
  value       = var.always_on
}
