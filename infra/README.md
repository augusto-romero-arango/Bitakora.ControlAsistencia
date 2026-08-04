# Infraestructura — ControlAsistencias

Infraestructura en Azure gestionada con Terraform. El ambiente `dev` despliega 4 Azure Functions (una por dominio), un Service Bus, Application Insights y un Service Plan Consumption.

## Prerequisitos

| Herramienta | Version minima | Instalacion |
|---|---|---|
| Terraform | >= 1.6 | `brew install terraform` |
| Azure CLI | >= 2.50 | `brew install azure-cli` |
| GitHub CLI | >= 2.0 | `brew install gh` |

Autenticarse antes de comenzar:

```bash
az login
gh auth login
```

## Estructura

```
infra/
  modules/          # Modulos reutilizables (resource-group, storage, function-app, etc.)
  environments/
    dev/            # Ambiente de desarrollo
      main.tf       # Orquesta los modulos
      providers.tf  # Backend azurerm + provider
      variables.tf  # Variables de entrada
      terraform.tfvars  # Valores del ambiente (subscription_id)
      outputs.tf    # Outputs (nombres de recursos)
  scripts/
    bootstrap-backend.sh  # Crea el RG + storage account para el tfstate
```

## Paso 1 — Bootstrap del backend de Terraform

El backend almacena el `tfstate` en Azure Storage. Debe crearse una sola vez antes del primer `apply`.

```bash
./infra/scripts/bootstrap-backend.sh --env dev
```

El script crea:
- Resource Group: `rg-controlasistencias-tfstate`
- Storage Account: `stcatfstatedev`
- Blob Container: `tfstate`

> Ver issue #96 para el contexto completo del bootstrap.

## Paso 2 — Configurar `terraform.tfvars`

En `infra/environments/dev/`, crear o verificar el archivo `terraform.tfvars`:

```hcl
subscription_id = "<tu-subscription-id-de-azure>"
```

Obtener el subscription ID:

```bash
az account show --query id -o tsv
```

## Paso 3 — Primer `terraform apply`

Usar el pipeline IaC desde la raiz del proyecto:

```bash
./scripts/iac-pipeline.sh <numero-de-issue> --env dev
```

El `plan` real corre en CI sobre el PR y el `apply` real al mergear a `main` (MEF-ADR-0021, MEF-ADR-0022): el desarrollador no accede al tfstate ni ejecuta `apply` en local. La revision local es **estatica**, sin backend ni credenciales de Azure:

```bash
cd infra/environments/dev
terraform fmt -check -recursive ../..
terraform init -backend=false -input=false
terraform validate
```

## Paso 4 — Configurar CI/CD

El workflow `.github/workflows/infra-ci.yml` ejecuta `terraform plan` automaticamente en PRs que modifican `infra/`. Requiere un Service Principal con permisos en la suscripcion.

### Crear el Service Principal (una sola vez)

```bash
./scripts/setup-github-ci.sh <subscription-id>
```

El script imprime 3 valores, todos identificadores (ninguno es una credencial). Configurarlos como secrets en GitHub:

**Settings > Secrets and variables > Actions > New repository secret**

| Secret | Descripcion |
|---|---|
| `AZURE_CLIENT_ID` | Client ID del Service Principal |
| `AZURE_TENANT_ID` | Tenant ID de Azure AD |
| `AZURE_SUBSCRIPTION_ID` | ID de la suscripcion Azure |

El Service Principal necesita ademas `Storage Blob Data Contributor` sobre la storage account del tfstate (`stcatfstatedev`): con el backend keyless (ver Mantenimiento) el state se escribe y se bloquea por AAD, no con la account key.

### Verificar que el CI funciona

Crear un PR que modifique cualquier archivo en `infra/`. El workflow debe ejecutarse y postar el plan como comentario en el PR.

### CD — Terraform Apply automatico

El workflow `.github/workflows/infra-cd.yml` ejecuta `terraform apply` automaticamente cuando se mergea a `main` un commit que modifique archivos en `infra/`.

**Flujo al mergear un PR de infraestructura:**

1. GitHub detecta el push a `main` con cambios en `infra/`
2. Se ejecuta `terraform init` + `terraform plan` (re-planifica — el estado puede haber cambiado desde que se aprobo el PR)
3. Si el plan es exitoso, ejecuta `terraform apply -auto-approve`
4. Si cualquier step falla, el workflow falla y GitHub envia email de notificacion al autor del commit

**Por que se re-planifica antes del apply:**

El `tfplan` generado en el PR no se reutiliza. El estado remoto puede haber cambiado entre la aprobacion del PR y el merge (otro PR mergeado antes, cambios manuales en Azure, etc.). Re-planificar garantiza que el apply refleja el estado actual.

**Ver los logs del apply:**

En GitHub: **Actions** > seleccionar el workflow `Infra CD - Terraform Apply` > click en el run correspondiente.

## Mantenimiento

### Autenticacion keyless (sin rotacion de secretos)

El backend del tfstate es **keyless por AAD** (`use_azuread_auth = true` en el bloque `backend "azurerm"` de `providers.tf`, MEF-ADR-0025 decision #8): el plano de datos del blob se autentica por Microsoft Entra ID/RBAC en vez de la account key de la storage account, que el backend resolvia via `listKeys` en cada corrida. La identidad de CI necesita `Storage Blob Data Contributor` sobre `stcatfstatedev`.

No hay account key ni client secret que rotar: la autenticacion del SP de CI hacia AAD es por OIDC/token federado (MEF-ADR-0022), que no expira como una credencial estatica. La migracion de los workflows a OIDC la completa el issue #297; el backend keyless de aca es independiente de ese paso y ya elimina la account key del camino.

Si tenes un `.terraform/` previo a este cambio (creado antes de que el backend declarara `use_azuread_auth`), `terraform init` va a detectar el cambio de configuracion del backend y va a pedir reconfigurar:

```bash
cd infra/environments/dev
terraform init -reconfigure
```

Esto no migra ni mueve el state -- sigue siendo el mismo resource group, cuenta, contenedor y key. Solo cambia *como* se autentica el plano de datos.

### Agregar un nuevo ambiente

1. Crear `infra/environments/<nombre>/` copiando la estructura de `dev/`
2. Ajustar las variables en `terraform.tfvars` y `variables.tf`
3. Ejecutar el bootstrap del backend para el nuevo ambiente
4. Actualizar `.github/workflows/infra-ci.yml` y `.github/workflows/infra-cd.yml` para incluir el nuevo `working-directory`
