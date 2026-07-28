variable "subscription_id" {
  description = "ID de la suscripcion de Azure"
  type        = string
}

variable "project" {
  description = "Nombre corto del proyecto (sin espacios)"
  type        = string
  default     = "controlasistencias"
}

variable "environment" {
  description = "Nombre del ambiente"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Region de Azure"
  type        = string
  default     = "eastus2"
}

variable "project_short" {
  description = "Nombre corto del proyecto para recursos con limite de caracteres"
  type        = string
  default     = "asist"
}

variable "postgresql_admin_password" {
  description = "Contrasena del administrador de PostgreSQL"
  type        = string
  sensitive   = true
}

# Worker de proyecciones (opt-in, MEF-ADR-0034 seccion 8; issue #234). Placeholder publico
# hasta que un pipeline de CI de imagen (fuera de alcance de este issue) construya y empuje
# la imagen real a module.container_registry.login_server; en ese momento, sobreescribe
# este default via terraform.tfvars o TF_VAR_projections_worker_image.
variable "projections_worker_image" {
  description = "Imagen del worker de proyecciones (Bitakora.ControlAsistencia.Projections)"
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

locals {
  prefix       = "${var.project}-${var.environment}"
  prefix_short = "${var.project_short}-${var.environment}"

  tags = {
    proyecto   = var.project
    ambiente   = var.environment
    gestionado = "terraform"
  }
}
