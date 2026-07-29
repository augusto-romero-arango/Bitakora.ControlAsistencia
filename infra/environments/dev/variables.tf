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

# Worker de proyecciones (opt-in, MEF-ADR-0034 seccion 8; issue #234). Este default es
# SOLO BOOTSTRAP: alimenta unicamente la creacion inicial (`create`) del Container App
# (issue #249). A partir del primer `az containerapp update --image` que el pipeline de
# imagen real corra contra module.container_registry.login_server (issue #236), este
# valor deja de reflejar la imagen activa -- el `lifecycle.ignore_changes` del modulo
# container-app (template[0].container[0].image) hace que `terraform plan` deje de
# comparar el estado remoto contra este default, precisamente para no proponer revertir
# la imagen real al placeholder. NO se sobreescribe via terraform.tfvars ni via
# TF_VAR_projections_worker_image (esa era la nota original, descartada en #249: ver el
# camino (b) descartado ahi, con el hallazgo de que *.tfvars esta en .gitignore y de que
# infra-cd.yml no tiene workflow_dispatch/inputs). El rollback de imagen se hace con
# `az containerapp update --image` o `az containerapp revision activate`, nunca
# revirtiendo este HCL.
variable "projections_worker_image" {
  description = "Imagen de bootstrap del worker de proyecciones (Bitakora.ControlAsistencia.Projections); solo gobierna el create inicial, ver la nota de arriba"
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
