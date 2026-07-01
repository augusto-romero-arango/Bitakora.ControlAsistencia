# ADR-0026: Custodia de secretos (connection strings en Key Vault)

## Estado

Aceptado

## Contexto

El harness (Mefisto) aceptó el 2026-07-01 el **ADR-0025 "Custodia de secretos"** (`docs/adr/0025-custodia-de-secretos.md` del plugin `mefisto@augusto-romero-arango-harness`, resuelto vía `.claude/pipeline/.plugin-root`). Su doctrina raíz: **ningún secreto ni key debe viajar en texto plano en los app settings de una Function App, ni materializarse en el state de Terraform**. Todo secreto se custodia en el Key Vault del bounded context y se referencia con `@Microsoft.KeyVault(SecretUri=...)` versionless, o se resuelve por identidad administrada cuando el runtime lo exige antes de poder resolver referencias de Key Vault. ADR-0025 del harness generaliza su ADR-0024 decisión #6, que sólo cubría las cadenas de Azure Service Bus.

Una auditoría de nuestro proyecto frente a esa doctrina encontró que **los cuatro secretos que ADR-0025 clasifica están hoy en texto plano** en `infra/environments/dev/main.tf`:

- Cadena de Azure Service Bus (`SERVICE_BUS_CONNECTION`), desde `module.service_bus.default_primary_connection_string`.
- Password de PostgreSQL embebido en `MartenConnectionString` (`...Password=${var.postgresql_admin_password}...`).
- Connection string de Application Insights (`APPLICATIONINSIGHTS_CONNECTION_STRING`, incluye la instrumentation key), desde `module.monitoring.connection_string`.
- Access key de la Storage Account del host (`AzureWebJobsStorage`), vía `storage_account_access_key` nativo del módulo `function-app`.

Cualquiera con lectura del recurso Function App, o del state de Terraform donde el valor también se materializa, ve estos secretos en claro. Además, a diferencia del harness, **no tenemos provisionado ningún Key Vault ni módulo Terraform** para custodiarlos; ni siquiera adoptamos ADR-0024 decisión #6.

Este ADR **no toca código ni infra**: registra formalmente la adopción de la doctrina del harness en el proyecto y fija el mapa concreto de secretos que los issues de infra implementan (#195 provisiona el vault; #196 conmuta las referencias; #197 migra el storage a identidad administrada).

## Decisión

### 1. Adoptamos la doctrina del harness por referencia, sin duplicarla

Adoptamos **ADR-0025 del harness** como doctrina de manejo de secretos del proyecto. No reproducimos su contenido: es la fuente de verdad de la regla, y por convención de este proyecto los ADRs no duplican reglas de ADRs del harness (los consultan, aplican y documentan sus desviaciones). Este ADR registra únicamente **la adaptación propia**: el mapa concreto de secretos, las divergencias con el harness y los caveats de nuestra migración brownfield.

El principio que adoptamos, enunciado: **un app setting nunca contiene el valor de un secreto** (cadena de conexión con password, access key, instrumentation key, token). Lleva una referencia `@Microsoft.KeyVault(SecretUri=...)` versionless, o el secreto se resuelve por identidad administrada. El principio aplica por igual al recurso desplegado y al **state de Terraform**: Terraform no materializa el valor de ningún secreto.

### 2. Mapa concreto de secretos del proyecto

| Secreto | App setting que lo consume | Nombre del secreto en Key Vault | Mecanismo |
|---|---|---|---|
| Cadena de Azure Service Bus | `SERVICE_BUS_CONNECTION` | `service-bus-connection` | Referencia Key Vault |
| Password de PostgreSQL (dentro de `MartenConnectionString`) | `MartenConnectionString` | `marten-connection` | Referencia Key Vault |
| Connection string de Application Insights | `APPLICATIONINSIGHTS_CONNECTION_STRING` | `app-insights-connection` | Referencia Key Vault |
| Access key de Storage del host | `AzureWebJobsStorage` | (no aplica) | Identidad administrada |

Los nombres `marten-connection` y `app-insights-connection` se respetan tal cual el harness los ancla en `agents/infra-base-scaffolder.md`. `service-bus-connection` es la **decisión propia** para nuestra cadena única de ASB (ver decisión #4).

### 3. Las claves de app setting no cambian; sólo su valor

El código de ambos dominios (`Program.cs`) lee `SERVICE_BUS_CONNECTION` y `MartenConnectionString` como variables de entorno por su nombre. Migrar la custodia **no requiere tocar C#**: las claves de app setting se conservan idénticas; sólo el **valor** pasa de literal a referencia `@Microsoft.KeyVault(SecretUri=...)`.

### 4. Divergencia con la topología de ADR-0024 del harness

**No adoptamos la topología de bus interno/externo de ADR-0024** (harness). Usamos un único namespace `sb-${prefix}` y un único app setting `SERVICE_BUS_CONNECTION`, sin bloque `serviceBus` en `.claude/harness.config.json`. Por eso nuestro mapa de secretos difiere de la implementación de referencia del harness (`infra-base-scaffolder`, que usa prefijos `sbint-` y app settings `SERVICE_BUS_CONNECTION_<ALIAS>`): tenemos **un** secreto `service-bus-connection`, no uno por alias de bus. Esa migración a la topología interno/externo queda **fuera del alcance** de este ADR y de los issues de custodia.

### 5. `AzureWebJobsStorage` va por identidad administrada, no por Key Vault

El runtime de Azure Functions necesita el storage del host **al arrancar**, antes de poder resolver referencias `@Microsoft.KeyVault(...)`. Por eso `AzureWebJobsStorage` no se custodia en Key Vault: se accede por **identidad administrada** (`storage_uses_managed_identity = true` + roles de datos de Storage a la managed identity de la Function App). No es un secreto custodiado, es acceso identity-based, coherente con el espíritu OIDC del proyecto.

### 6. El valor de cada secreto se siembra administrativamente

El **valor** de cada secreto de Key Vault se coloca de forma administrativa fuera del ciclo de Terraform y del repo (`az keyvault secret set`), nunca por Terraform. El proyecto provisiona por Terraform únicamente (a) la referencia en app settings y (b) el rol **Key Vault Secrets User** de la managed identity de la Function App. Consecuencia operativa: entre provisionar el vault (#195) y conmutar las referencias (#196) hay una siembra manual obligatoria; conmutar antes de sembrar deja a las Function Apps sin poder resolver las referencias al arrancar.

## Consecuencias

**Positivas**

- App settings sin secretos en claro, ni en el recurso desplegado ni en el state de Terraform.
- Almacén único: el Key Vault del proyecto concentra los secretos y sirve de base para secretos futuros (API keys de terceros, etc.) sin volver a decidir el mecanismo.
- Alineado con la best-practice de Azure: referencias de Key Vault para app settings; identidad administrada para el storage del host.
- No requiere cambios de código: las claves de app setting se conservan.

**Negativas**

- Más RBAC y referencias que provisionar: cada secreto suma una referencia y, donde aplique, un role assignment de datos. Crear esos role assignments exige que el principal que aplica tenga permiso `Microsoft.Authorization/roleAssignments/write` (Owner o User Access Administrator); el principal de CI actual sólo tiene Contributor, que no basta.
- Siembra administrativa: el valor de cada secreto es una acción manual post-`apply`.
- Postgres y App Insights siguen siendo secretos custodiados (no identity-based); se mantiene la deuda de rotación hasta que existan alternativas por Entra ID.

**Fuera de alcance / trabajo diferido**

- Migración a la topología de bus interno/externo de ADR-0024 del harness (decisión #4).
- Migración de ASB, Postgres y App Insights a identidad administrada, cuando exista soporte viable.
- Rotación automatizada de los secretos.

## Referencias

- ADR-0025 del harness: "Custodia de secretos" — doctrina raíz que este ADR adopta por referencia.
- ADR-0024 del harness, decisión #6: custodia de cadenas de ASB — instancia de la doctrina; documentamos que NO adoptamos su topología interno/externo.
- ADR-0021 del harness: infraestructura base — unicidad global del nombre del Key Vault y `prevent_destroy`. (Nota: el número 0021 del harness NO guarda relación con la numeración local; las dos series son independientes.)
- ADR-0022 del harness: autenticación de CI por OIDC — mismo espíritu identity-based que la ruta de identidad administrada para Storage.
- Implementación de referencia: `agents/infra-base-scaffolder.md` del plugin (módulos `key-vault` y `function-app`; nombres de secreto anclados y roles de datos de Storage).
- Issues del proyecto: #195 (provisionar el Key Vault), #196 (conmutar referencias de ASB, Postgres y App Insights), #197 (identidad administrada para `AzureWebJobsStorage`).
- "Use Key Vault references for App Service and Azure Functions". https://learn.microsoft.com/azure/app-service/app-service-key-vault-references
- "Connect to host storage with an identity" (Azure Functions). https://learn.microsoft.com/azure/azure-functions/functions-reference#connecting-to-host-storage-with-an-identity

## Control de cambios

- 2026-07-01: creación. Adopta la doctrina de custodia de secretos del harness (su ADR-0025) y fija el mapa concreto de secretos del proyecto, la divergencia con la topología de ADR-0024 y los caveats de la migración brownfield. Numerado 0026 por ser el siguiente libre de la serie local (la serie local ya tenía 0025; el 0025 del harness es de otra serie).
