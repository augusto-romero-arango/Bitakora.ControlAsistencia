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

locals {
  prefix      = "${var.project}-${var.environment}"
  prefix_short = "${var.project_short}-${var.environment}"

  tags = {
    proyecto   = var.project
    ambiente   = var.environment
    gestionado = "terraform"
  }
}
