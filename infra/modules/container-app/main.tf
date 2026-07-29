# ------------------------------------------------------------------------------------
# GOBIERNO DE LA IMAGEN: la imagen de este Container App la gobierna CI, no Terraform
# (issue #249). `var.image` alimenta UNICAMENTE la creacion inicial (bootstrap con el
# placeholder publico de var.projections_worker_image, ver la nota de esa variable en
# infra/environments/dev/variables.tf); a partir de ahi el pipeline de aplicacion (el
# que corre `az containerapp update --image ...` en cada push a `main` sobre la imagen
# real) es el unico dueno del atributo `image`. El bloque `lifecycle` de abajo declara
# `ignore_changes = [template[0].container[0].image]` para que Terraform deje de
# proponer revertir la imagen desplegada por CI al placeholder del HCL (HashiCorp,
# Terraform, "The lifecycle Meta-Argument" -- seccion ignore_changes: "Terraform
# considers the arguments corresponding to the given attribute names when planning a
# create operation, but are ignored when planning an update operation").
#
# RESTRICCION CONOCIDA: `lifecycle` no admite expresiones dinamicas -- no se puede
# condicionar `ignore_changes` a una variable ni hacerlo opt-in por flag. Esto significa
# que la semantica "la imagen la publica CI" aplica a CUALQUIER Container App que este
# modulo instancie en el futuro, no solo al worker de proyecciones. Hoy el modulo tiene
# un unico consumidor (module.container_app del entorno dev, el worker de proyecciones)
# y esa semantica es exactamente la suya. Si en el futuro se necesita un Container App
# cuya imagen SI gobierne Terraform (por ejemplo, sin pipeline de CI propio), hay que
# separar ese caso en un modulo distinto ANTES de instanciarlo con este.
#
# ROLLBACK DE IMAGEN: con Terraform fuera del gobierno de la imagen, el rollback NO se
# hace revirtiendo HCL ni corriendo `terraform apply`. Se hace con:
#   az containerapp update -n <nombre> -g <resource-group> --image <acr>/<repo>:<sha-anterior>
# o activando una revision previa con `az containerapp revision activate` (Microsoft
# Learn, "Revisions in Azure Container Apps").
# ------------------------------------------------------------------------------------

variable "name" {
  description = "Nombre del Container App (2-32 chars, minusculas/numeros/guiones, empieza con letra y termina alfanumerico -- Microsoft.App/containerApps, scope resource group; verificado contra Microsoft Learn, 'Naming rules and restrictions for Azure resources')"
  type        = string
}

variable "resource_group_name" {
  description = "Nombre del resource group"
  type        = string
}

variable "container_app_environment_id" {
  description = "ID del Container App Environment (modulo container-app-environment)"
  type        = string
}

variable "image" {
  description = "Referencia completa de la imagen del contenedor (registry/repositorio:tag). El wiring del entorno la alimenta desde var.projections_worker_image -- ver la nota de bootstrap ahi sobre el placeholder inicial"
  type        = string
}

variable "min_replicas" {
  description = "Replicas minimas. SIEMPRE >= 1 (validado abajo): sin ingress, Azure no reactiva un Container App que escala a 0 (MEF-ADR-0034 seccion 8)"
  type        = number
  default     = 1

  validation {
    condition     = var.min_replicas >= 1
    error_message = "min_replicas debe ser >= 1: sin ingress, un Container App que escala a 0 no tiene forma de reactivarse (MEF-ADR-0034 seccion 8; Microsoft Learn, 'Set scaling rules in Azure Container Apps')."
  }
}

variable "max_replicas" {
  description = "Replicas maximas. HotCold (el daemon de Marten que corre dentro del contenedor) tolera mas de una replica activa via eleccion de lider sobre advisory locks de Postgres (MEF-ADR-0034 seccion 2)"
  type        = number
  default     = 1
}

variable "cpu" {
  description = "vCPU del contenedor. Debe formar una combinacion valida con memory en el plan Consumption (Microsoft Learn, 'Containers in Azure Container Apps' -- Configuration): incrementos de 0.25, y memory exactamente cpu*2 en Gi (ver la precondition del recurso)"
  type        = number
  default     = 0.25

  # Azure no admite cualquier valor de cpu: la tabla de asignaciones solo lista
  # incrementos de 0.25 (Microsoft Learn, "Containers in Azure Container Apps" --
  # Configuration). El techo de 2.0 es el del entorno que crea el modulo
  # container-app-environment, que no declara workload profiles y por tanto es un
  # entorno *Consumption only*: "Apps using the Consumption plan in a Consumption only
  # environment are limited to a maximum of 2 cores and 4Gi of memory" (misma pagina).
  # Se valida aca -- y no solo en el apply -- porque quien escribe el HCL no tiene
  # credenciales de Azure (MEF-ADR-0021/0022): un valor invalido debe fallar en el
  # `terraform validate` local, no a mitad del apply de CI.
  validation {
    condition     = var.cpu >= 0.25 && var.cpu <= 2.0 && var.cpu * 4 == floor(var.cpu * 4)
    error_message = "cpu debe ser un multiplo de 0.25 entre 0.25 y 2.0 (Microsoft Learn, 'Containers in Azure Container Apps' -- Configuration; el techo de 2.0 aplica a un entorno Consumption only, que es el que crea el modulo container-app-environment)."
  }
}

variable "memory" {
  description = "Memoria del contenedor. Debe formar una combinacion valida con cpu en el plan Consumption"
  type        = string
  default     = "0.5Gi"
}

variable "env_vars" {
  description = "Variables de entorno NO sensibles (ej. nivel de log, nombre del ambiente). Ninguna cadena de conexion va aqui -- ver key_vault_secret_refs (MEF-ADR-0025 / ADR-0026 local)"
  type        = map(string)
  default     = {}
}

variable "key_vault_secret_refs" {
  description = "Variables de entorno sensibles resueltas por Key Vault reference con la identidad UserAssigned de var.user_assigned_identity_id (MEF-ADR-0025 / ADR-0026 local). Clave = nombre interno del secret block; env_var_name = nombre de la variable de entorno que lo consume; key_vault_secret_id = URI del secreto (versionless), NUNCA el valor en claro"
  type = map(object({
    env_var_name        = string
    key_vault_secret_id = string
  }))
  default = {}
}

variable "registry_server" {
  description = "login_server del Container Registry (modulo container-registry) del que este Container App extrae la imagen"
  type        = string
}

variable "user_assigned_identity_id" {
  description = "Resource ID de la UNICA identidad UserAssigned de este Container App, con rol AcrPull sobre el Container Registry y Key Vault Secrets User sobre el Key Vault del BC (ambos otorgados por el wiring del entorno ANTES de instanciar este modulo). NUNCA \"System\": el bloque registry.identity exige un Resource ID UserAssigned, y una Key Vault reference con \"System\" no puede resolverse en la creacion de la app porque esa identidad no existe todavia (MEF-ADR-0034 seccion 8)"
  type        = string
}

variable "tags" {
  description = "Tags comunes del proyecto"
  type        = map(string)
  default     = {}
}

# El worker de proyecciones en si (MEF-ADR-0034 secciones 1-8): sin bloque `ingress`
# (nadie le hace requests), revision_mode = "Single", identidad administrada para leer
# secretos del Key Vault del BC (bloque `secret`, MEF-ADR-0025 / ADR-0026 local) y
# min_replicas >= 1 exigido por validacion -- sin ingress, un Container App que escala
# a 0 no tiene forma de reactivarse.
#
# Identidad UNICA UserAssigned para el pull del ACR y para las Key Vault references
# (punto abierto de MEF-ADR-0034 resuelto en el ADR): el bloque registry.identity exige
# el Resource ID de una identidad User Assigned (el literal "System" no es valido ahi),
# y una Key Vault reference con "System" no puede resolverse al CREAR la app porque esa
# identidad no existe hasta despues de crearla. Ningun role assignment posterior lo
# arregla. La identidad SystemAssigned sigue habilitada (output principal_id) para
# cualquier RBAC futuro que si pueda asignarse despues de crear la app, pero no es quien
# lee el Key Vault ni quien pulea la imagen.
resource "azurerm_container_app" "this" {
  name                         = var.name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = var.container_app_environment_id
  revision_mode                = "Single"

  identity {
    type         = "SystemAssigned, UserAssigned"
    identity_ids = [var.user_assigned_identity_id]
  }

  registry {
    server   = var.registry_server
    identity = var.user_assigned_identity_id
  }

  # identity = la UserAssigned, NUNCA "System": el plano de control resuelve la
  # referencia dentro del PUT que crea esta app, cuando la identidad SystemAssigned
  # aun no existe (Microsoft Learn, "Manage secrets in Azure Container Apps").
  dynamic "secret" {
    for_each = var.key_vault_secret_refs
    content {
      name                = secret.key
      identity            = var.user_assigned_identity_id
      key_vault_secret_id = secret.value.key_vault_secret_id
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = var.name
      image  = var.image
      cpu    = var.cpu
      memory = var.memory

      dynamic "env" {
        for_each = var.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.key_vault_secret_refs
        content {
          name        = env.value.env_var_name
          secret_name = env.key
        }
      }
    }
  }

  # Sin bloque `ingress` a proposito (MEF-ADR-0034 seccion 8): el worker no recibe
  # trafico HTTP/TCP -- lee eventos directamente de Postgres (async daemon de Marten)
  # y escribe proyecciones; nunca es el destino de un request.

  tags = var.tags

  lifecycle {
    # Azure solo admite pares cpu/memory de una tabla cerrada: memory es SIEMPRE cpu*2
    # en Gi (0.25->0.5Gi, 0.5->1.0Gi, 0.75->1.5Gi, ... 2.0->4.0Gi). Un par fuera de la
    # tabla no lo detecta `terraform validate` -- lo rechaza el plano de control en
    # medio del apply. Como el apply corre en CI sin nadie mirando (MEF-ADR-0022), la
    # relacion se verifica aca, en el plan, con un mensaje que dice que poner.
    # Fuente: Microsoft Learn, "Containers in Azure Container Apps" -- Configuration.
    precondition {
      condition     = var.memory == format("%.1fGi", var.cpu * 2)
      error_message = "Par cpu/memory invalido: con cpu = ${var.cpu}, memory debe ser exactamente \"${format("%.1fGi", var.cpu * 2)}\" (Azure exige memory = cpu*2 Gi; Microsoft Learn, 'Containers in Azure Container Apps' -- Configuration)."
    }

    # max_replicas < min_replicas es un apply roto garantizado; se atrapa antes.
    precondition {
      condition     = var.max_replicas >= var.min_replicas
      error_message = "max_replicas (${var.max_replicas}) no puede ser menor que min_replicas (${var.min_replicas})."
    }

    # La imagen la gobierna CI, no Terraform (issue #249, ver la nota de cabecera del
    # modulo). `var.image` solo alimenta el `create` inicial (bootstrap con placeholder);
    # a partir de ahi `az containerapp update --image` de CI es el dueno del atributo, y
    # sin este ignore_changes cada `plan` posterior propondria revertirlo al placeholder
    # del HCL. Aplica a cualquier consumidor futuro del modulo (lifecycle no admite
    # expresiones dinamicas ni opt-in por flag) -- ver la restriccion documentada arriba.
    ignore_changes = [
      template[0].container[0].image
    ]
  }
}

output "id" {
  value = azurerm_container_app.this.id
}

output "name" {
  value = azurerm_container_app.this.name
}

output "principal_id" {
  description = "Principal ID de la identidad SystemAssigned de este Container App. NO es quien lee el Key Vault (eso va por var.user_assigned_identity_id, ver la nota del modulo): queda expuesto para cualquier RBAC futuro que si pueda asignarse despues de crear la app"
  value       = azurerm_container_app.this.identity[0].principal_id
}
