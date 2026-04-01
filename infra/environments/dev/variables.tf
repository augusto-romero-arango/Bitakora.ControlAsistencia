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

variable "dominios" {
  description = "Lista de dominios de negocio (una Function App por dominio)"
  type        = list(string)
  default     = ["calculo-horas", "depuracion", "programacion", "empleados"]
}

locals {
  prefix = "${var.project}-${var.environment}"

  tags = {
    proyecto   = var.project
    ambiente   = var.environment
    gestionado = "terraform"
  }
}
