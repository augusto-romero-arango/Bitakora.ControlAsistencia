data "azurerm_client_config" "current" {}

module "resource_group" {
  source   = "../../modules/resource-group"
  name     = "rg-${local.prefix}"
  location = var.location
  tags     = local.tags
}


# Un Service Plan por dominio (ADR-0020): aisla el computo de cada dominio.
# Mismas caracteristicas que el plan compartido anterior: Linux, SKU B1.
module "service_plan_programacion" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-programacion"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  tags                = local.tags
}

module "service_plan_control_horas" {
  source              = "../../modules/service-plan"
  name                = "asp-${local.prefix_short}-control-horas"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  sku_name            = "B1"
  tags                = local.tags
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
    "dia-calculado" = {
      subscriptions = [
        {
          name                = "smoke-tests"
          correlation_filter  = null
          default_message_ttl = "PT5M"
        }
      ]
    }
    # ADR del marco (decision #3): todo evento privado cruza fisicamente el
    # ASB interno, aun cuando productor (RegistrarMarcacion) y consumidor
    # (AdicionarMarcacion) viven en el mismo Function App (ControlHoras).
    "marcacion-registrada" = {
      subscriptions = [
        {
          name               = "control-horas-escucha-marcacion"
          correlation_filter = null
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

resource "random_string" "storage_suffix_programacion" {
  length  = 6
  special = false
  upper   = false
}

module "storage_programacion" {
  source              = "../../modules/storage"
  name                = "stprogramacion${var.environment}${random_string.storage_suffix_programacion.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "function_app_programacion" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-programacion"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_programacion.id
  storage_account_name           = module.storage_programacion.name
  app_insights_connection_string = module.monitoring.connection_string
  app_settings = {
    SERVICE_BUS_CONNECTION = local.service_bus_connection_kv_ref
    DOMINIO                = "programacion"
    MartenConnectionString = local.marten_connection_kv_ref
  }
  tags = local.tags
}

resource "random_string" "storage_suffix_control_horas" {
  length  = 6
  special = false
  upper   = false
}

module "storage_control_horas" {
  source              = "../../modules/storage"
  name                = "stcontrolhoras${var.environment}${random_string.storage_suffix_control_horas.result}"
  resource_group_name = module.resource_group.name
  location            = module.resource_group.location
  tags                = local.tags
}

module "function_app_control_horas" {
  source                         = "../../modules/function-app"
  name                           = "func-${local.prefix_short}-control-horas"
  resource_group_name            = module.resource_group.name
  location                       = module.resource_group.location
  service_plan_id                = module.service_plan_control_horas.id
  storage_account_name           = module.storage_control_horas.name
  app_insights_connection_string = module.monitoring.connection_string
  app_settings = {
    SERVICE_BUS_CONNECTION = local.service_bus_connection_kv_ref
    DOMINIO                = "control-horas"
    MartenConnectionString = local.marten_connection_kv_ref
  }
  tags = local.tags
}

# RBAC data-plane (ADR-0024 decision #6): la managed identity de cada Function
# App necesita "Key Vault Secrets User" para resolver las referencias
# @Microsoft.KeyVault(...) de sus app settings.
resource "azurerm_role_assignment" "kv_secrets_user_programacion" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.function_app_programacion.principal_id
}

resource "azurerm_role_assignment" "kv_secrets_user_control_horas" {
  scope                = module.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.function_app_control_horas.principal_id
}

# Storage por identidad administrada (ADR-0025 decision #3): AzureWebJobsStorage
# no puede ir por referencia de Key Vault (el runtime necesita el storage al
# arrancar, antes de resolver referencias). La managed identity de cada Function
# App necesita los tres roles de datos de Storage sobre su propia cuenta.
locals {
  storage_data_roles = toset([
    "Storage Blob Data Owner",
    "Storage Queue Data Contributor",
    "Storage Table Data Contributor",
  ])
}

resource "azurerm_role_assignment" "storage_data_programacion" {
  for_each             = local.storage_data_roles
  scope                = module.storage_programacion.id
  role_definition_name = each.value
  principal_id         = module.function_app_programacion.principal_id
}

resource "azurerm_role_assignment" "storage_data_control_horas" {
  for_each             = local.storage_data_roles
  scope                = module.storage_control_horas.id
  role_definition_name = each.value
  principal_id         = module.function_app_control_horas.principal_id
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

