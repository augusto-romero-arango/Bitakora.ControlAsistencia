data "azurerm_client_config" "current" {}

module "resource_group" {
  source   = "../../modules/resource-group"
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}


module "monitoring" {
  source              = "../../modules/monitoring"
  name                = local.prefix
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "postgresql" {
  source                 = "../../modules/postgresql"
  name                   = "psql-${local.prefix_short}"
  resource_group_name    = module.resource_group.name
  location               = "centralus" # eastus2 restringida para PostgreSQL Flexible Server
  zone                   = "2"         # zona asignada por Azure al crear el servidor
  administrator_login    = "pgadmin"
  administrator_password = var.postgresql_admin_password
  database_name          = "controlasistencias"
  tags                   = local.tags
}

module "service_bus" {
  source              = "../../modules/service-bus"
  name                = "sb-${local.prefix}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku                 = "Standard"
  topics_config = {
    "programacion-turno-diario-solicitada" = {
      subscriptions = [
        {
          name               = "control-horas-escucha-programacion"
          correlation_filter = null
        },
        {
          name                = "smoke-tests"
          correlation_filter  = null
          default_message_ttl = "PT5M"
        }
      ]
    }
    # Issue #498: espejo de "programacion-turno-diario-solicitada" para la operacion inversa.
    # El consumidor real (ControlHoras) llega en issue posterior -- publicar sin consumidor es
    # valido (MEF-ADR-0013); la subscription smoke-tests cubre la publicacion mientras tanto.
    "cancelacion-turno-diario-solicitada" = {
      subscriptions = [
        {
          name               = "control-horas-escucha-cancelacion"
          correlation_filter = null
        },
        {
          name                = "smoke-tests"
          correlation_filter  = null
          default_message_ttl = "PT5M"
        }
      ]
    }
    "dia-depurado" = {
      subscriptions = [
        {
          name               = "control-horas-escucha-dia-depurado"
          correlation_filter = null
        },
        {
          name                = "smoke-tests"
          correlation_filter  = null
          default_message_ttl = "PT5M"
        }
      ]
    }
    # Issue #274: contrato de bus propio nacido de separar el doble rol de
    # MarcacionRegistrada (issue #270). El topic "marcacion-registrada" que
    # este reemplazo dejaba huerfano (sin productor ni consumidor) se retiro
    # en el issue #276 (MEF-ADR-0001: un topic sin evento no tiene razon de
    # existir).
    "registro-de-marcacion-creado" = {
      subscriptions = [
        {
          name               = "control-horas-escucha-registro-de-marcacion"
          correlation_filter = null
        },
        {
          # Issue #467: Sedes (dueno del dato dispositivo->sede->CC, MEF-ADR-0046) se suscribe al
          # mismo topic para resolver la sede de la marcacion.
          name               = "sedes-escucha-registro-de-marcacion"
          correlation_filter = null
        },
        {
          name                = "smoke-tests"
          correlation_filter  = null
          default_message_ttl = "PT5M"
        }
      ]
    }
    # Issue #467: resultado del enriquecimiento coreografiado que Sedes publica tras resolver la
    # ubicacion de una marcacion (MEF-ADR-0046). La subscription de ControlHoras la crea #463.
    "sede-de-marcacion-resuelta" = {
      subscriptions = [
        {
          # Issue #463 (MEF-ADR-0026, carrera B "dentro del topic"): dos resoluciones del mismo
          # colaborador convergen sobre el mismo cd: -- session-enabled serializa el fan-in. El
          # productor (Sedes) publica con PublishOptions.GroupId = CodigoColaborador (#467).
          name               = "control-horas-escucha-sede-de-marcacion-resuelta"
          correlation_filter = null
          requires_session   = true
        },
        {
          name                = "smoke-tests"
          correlation_filter  = null
          default_message_ttl = "PT5M"
        }
      ]
    }
  }
  tags = local.tags
}

# Almacen general de secretos del BC (ADR-0025 decision #5). El nombre del
# Key Vault es un endpoint DNS publico (*.vault.azure.net), unico en TODO
# Azure (ADR-0021): requiere sufijo de unicidad global igual que PostgreSQL
# y Service Bus.
resource "random_string" "key_vault_suffix" {
  length  = 6
  special = false
  upper   = false
}

module "key_vault" {
  source              = "../../modules/key-vault"
  name                = "kv-${var.project_short}-${random_string.key_vault_suffix.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tenant_id           = data.azurerm_client_config.current.tenant_id
  tags                = local.tags
}

# Referencias versionless a Key Vault (ADR-0025 decision #2): el app setting
# lleva la referencia, no el valor. El valor lo siembra un admin (decision #6).
# Versionless (sin sufijo de version): toma la ultima al rotar el secreto.
locals {
  service_bus_connection_kv_ref = "@Microsoft.KeyVault(SecretUri=${module.key_vault.uri}secrets/service-bus-connection)"
  marten_connection_kv_ref      = "@Microsoft.KeyVault(SecretUri=${module.key_vault.uri}secrets/marten-connection)"
}

# Storage por identidad administrada (ADR-0025 decision #3): AzureWebJobsStorage no puede ir por
# referencia de Key Vault (el runtime necesita el storage al arrancar, antes de resolver
# referencias). Compartido por los cuatro dominios -- cada dominio-<nombre>.tf la referencia en su
# propio azurerm_role_assignment.storage_data_<nombre>.
locals {
  storage_data_roles = toset([
    "Storage Blob Data Owner",
    "Storage Queue Data Contributor",
    "Storage Table Data Contributor",
  ])
}

# ---- Worker de proyecciones (opt-in, MEF-ADR-0034 seccion 8; issue #234) ----
# Container App sin ingress: nadie le hace requests HTTP, solo lee eventos de Postgres
# y escribe proyecciones (async daemon de Marten, HotCold). El usuario acepto el
# trade-off de costo: min_replicas >= 1 corre siempre encendido, a diferencia de las
# Function Apps del write-side que escalan a demanda.

# El nombre de un Container Registry (*.azurecr.io) es unico en TODO Azure y admite
# SOLO alfanumerico (sin guiones), a diferencia de Postgres/Service Bus/Key Vault.
# Lleva el ambiente embebido igual que el otro recurso de nombre global-alfanumerico
# de este entorno (module.storage_*: "stcontrolhoras${var.environment}<sufijo>"), para
# que el registry de dev sea distinguible a simple vista del de un futuro staging/prod.
resource "random_string" "container_registry_suffix" {
  length  = 6
  special = false
  upper   = false
}

module "container_registry" {
  source              = "../../modules/container-registry"
  name                = "acr${var.project_short}${var.environment}${random_string.container_registry_suffix.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

# UNICA identidad del worker de proyecciones (MEF-ADR-0034 seccion 8): la usa tanto para
# pullear la imagen del ACR (registry.identity) como para resolver las Key Vault
# references de sus secretos (secret.identity). Se crea y se le otorgan LOS DOS roles
# ANTES de instanciar module.container_app -- un role assignment sobre una identidad
# SystemAssigned solo puede crearse DESPUES del recurso que la porta (patron de las
# Function Apps, arriba), y el plano de control de Container Apps resuelve la Key Vault
# reference dentro del PUT que crea la app, cuando esa identidad SystemAssigned aun no
# existiria.
resource "azurerm_user_assigned_identity" "projections_worker" {
  name                = "id-${local.prefix_short}-projections"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

resource "azurerm_role_assignment" "projections_worker_acr_pull" {
  scope                = module.container_registry.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.projections_worker.principal_id
  # La identidad se acaba de crear en este mismo apply: skip_service_principal_aad_check
  # evita que la verificacion contra Azure AD falle por lag de replicacion (MEF-ADR-0022
  # / nota operativa del modulo: la propagacion de RBAC puede tardar).
  skip_service_principal_aad_check = true
}

# Lectura de secretos del Key Vault del BC (CA-ADR-0026): mismo rol "Key Vault Secrets
# User" que reciben las Function Apps, pero otorgado a esta identidad UserAssigned y
# ANTES de crear el Container App -- no despues, como en las Function Apps. Con
# identity = "System" en el bloque secret, la referencia no podria resolverse en la
# creacion de la app porque esa identidad todavia no existiria (MEF-ADR-0034 seccion 8).
resource "azurerm_role_assignment" "projections_worker_kv_secrets_user" {
  scope                            = module.key_vault.id
  role_definition_name             = "Key Vault Secrets User"
  principal_id                     = azurerm_user_assigned_identity.projections_worker.principal_id
  skip_service_principal_aad_check = true
}

module "container_app_environment" {
  source                     = "../../modules/container-app-environment"
  name                       = "cae-${local.prefix_short}"
  resource_group_name        = module.resource_group.name
  location                   = module.resource_group.location
  log_analytics_workspace_id = module.monitoring.log_analytics_workspace_id
  tags                       = local.tags
}

locals {
  # URIs crudas del secreto (sin el envoltorio @Microsoft.KeyVault(SecretUri=...)): el
  # campo key_vault_secret_id del bloque `secret` de azurerm_container_app espera el URI
  # del secreto directamente, a diferencia del app setting de una Function App (que si
  # usa el envoltorio, ver locals de arriba). Reutilizan los mismos secretos fijos del BC
  # (marten-connection, app-insights-connection, ya sembrados en vivo): el worker de
  # proyecciones no crea ningun secreto nuevo (MEF-ADR-0034 seccion 2).
  projections_marten_connection_secret_uri       = "${module.key_vault.uri}secrets/marten-connection"
  projections_app_insights_connection_secret_uri = "${module.key_vault.uri}secrets/app-insights-connection"
}

module "container_app" {
  source                       = "../../modules/container-app"
  name                         = "ca-${local.prefix_short}"
  resource_group_name          = module.resource_group.name
  container_app_environment_id = module.container_app_environment.id
  image                        = var.projections_worker_image
  registry_server              = module.container_registry.login_server
  user_assigned_identity_id    = azurerm_user_assigned_identity.projections_worker.id

  # Dimensionamiento dev (issue #234, decision resuelta con el usuario): min_replicas = 1
  # fijo (no puede ser 0, MEF-ADR-0034 seccion 8), max_replicas = 1 (HotCold no acelera
  # con mas replicas), cpu/memory en el piso razonable para el daemon de proyecciones.
  min_replicas = 1
  max_replicas = 1
  cpu          = 0.25
  memory       = "0.5Gi"

  key_vault_secret_refs = {
    marten-connection = {
      env_var_name        = "MartenConnectionString"
      key_vault_secret_id = local.projections_marten_connection_secret_uri
    }
    app-insights-connection = {
      env_var_name        = "APPLICATIONINSIGHTS_CONNECTION_STRING"
      key_vault_secret_id = local.projections_app_insights_connection_secret_uri
    }
  }

  tags = local.tags

  # Fuerza a que AMBOS roles de la identidad UserAssigned esten asignados antes de que
  # exista el Container App: el AcrPull para que pueda pullear su imagen, y el Key Vault
  # Secrets User para que el plano de control resuelva las Key Vault references dentro
  # del PUT de creacion. Sin este depends_on, Terraform podria crear la app en paralelo
  # con los role assignments y la creacion fallaria con "Authorization failed".
  depends_on = [
    azurerm_role_assignment.projections_worker_acr_pull,
    azurerm_role_assignment.projections_worker_kv_secrets_user,
  ]
}

