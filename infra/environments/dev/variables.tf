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


locals {
  prefix      = "${var.project}-${var.environment}"
  prefix_func = "asist-${var.environment}"

  tags = {
    proyecto   = var.project
    ambiente   = var.environment
    gestionado = "terraform"
  }
}
