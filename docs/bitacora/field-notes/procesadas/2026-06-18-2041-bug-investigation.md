---
fecha: 2026-06-18
hora: 20:41
sesion: bug-investigator
tema: terraform apply de #172 (App Service Plan por dominio) falla con 409 al destruir el plan compartido que aun tiene apps asignadas
---

## Sintoma reportado
El `terraform apply` del issue #172 ("Provisionar un App Service Plan por dominio") fallo en
GitHub Actions. Run 27799876170, job 82267680160, workflow "Infra CD - Terraform Apply"
(`.github/workflows/infra-cd.yml`), disparado por push a `main` (commit 9dc3d0d, merge del PR #173).

## Investigacion

### Triage de deploy
No aplica el checklist de Function App (no es build/publish .NET): es un fallo de Terraform contra
Azure. Fui directo a leer el log real del job y a verificar el estado en Azure (solo lectura).

### Log del job (evidencia dura, `gh run view 27799876170 --log-failed`)
- El plan ejecutado fue exactamente: `Plan: 0 to add, 2 to change, 1 to destroy`.
  - 2 change in-place: ambas Function Apps cambian `service_plan_id` del plan viejo al nuevo.
  - 1 destroy: `module.service_plan.azurerm_service_plan.this` (asp-controlasistencias-dev),
    "because azurerm_service_plan.this is not in configuration".
- Terraform empezo por el destroy: `module.service_plan...: Destroying...` y fallo de inmediato:

  > Error: deleting App Service Plan (... Server Farm Name: "asp-controlasistencias-dev"):
  > unexpected status 409 (409 Conflict) ... "Server farm 'asp-controlasistencias-dev' cannot be
  > deleted because it has web app(s) func-asist-dev-control-horas,func-asist-dev-programacion
  > assigned to it." ExtendedCode 11003.

- El apply adquirio el lock sin problema ("Acquiring state lock... Releasing state lock") y libero
  al terminar. NO hubo colision de lock ni timeout de lock.

### Codigo correlacionado (commit 9dc3d0d, `main.tf` de #172)
- Define `module "service_plan_programacion"` (linea 11) y `module "service_plan_control_horas"`
  (linea 20), ambos B1 Linux, source `../../modules/service-plan`.
- Reapunta `service_plan_id` de cada Function App a su plan nuevo (lineas 101 y 133).
- ELIMINA `module "service_plan"` (el compartido). Por eso Terraform lo marca destroy.
- `modules/service-plan/main.tf` y `modules/function-app/main.tf` NO tienen
  `lifecycle { create_before_destroy }` ni ningun mecanismo que ordene el reassign antes del destroy.

### Estado real en Azure (suscripcion "Augusto" 50fc1901-..., solo lectura)
Nota operativa: el deploy corre contra la suscripcion "Augusto"; mi CLI local tiene "Azure Cosmos"
como default (por eso el primer `az` dio ResourceGroupNotFound). Consultas hechas con
`--subscription 50fc1901-...`.
- `asp-controlasistencias-dev` (B1): EXISTE, con 2 apps asignadas. NO se destruyo.
- `asp-asist-dev-control-horas` (B1): EXISTE, 0 apps.
- `asp-asist-dev-programacion` (B1): EXISTE, 0 apps.
- `func-asist-dev-control-horas`: Running, sigue apuntando a `asp-controlasistencias-dev` (plan VIEJO).
- `func-asist-dev-programacion`: Running, sigue apuntando a `asp-controlasistencias-dev` (plan VIEJO).

### Sospechosos del brief, evaluados
- Sospechoso #1 (orden destroy-antes-de-reassign): CONFIRMADO. Es la causa raiz.
- Sospechoso #2 (lock/apply concurrente): DESCARTADO. El log muestra lock adquirido y liberado
  limpiamente; el error es 409 de Azure, no lock de estado. El apply local previo (worktree #172)
  pudo haber CREADO los dos planes nuevos (estan creados con 0 apps), pero no causo el fallo de hoy.
- Auth / backend / quota / naming: DESCARTADOS. El plan corrio, refresco todo el estado, adquirio
  lock; el unico Error es el 409 de borrado.

## Diagnostico

### Causa raiz (confianza: ALTA)
El grafo de dependencias de Terraform no garantiza que las dos Function Apps se reasocien a los
planes nuevos ANTES de destruir el plac compartido. Como el plan viejo se marca destroy (ya no esta
en la config) sin `create_before_destroy`, Terraform intento borrarlo y Azure respondio 409: un
App Service Plan no se puede eliminar mientras tenga web apps asignadas (ExtendedCode 11003).
En este apply Terraform ejecuto PRIMERO el destroy del plan, antes de aplicar los `~ update in-place`
de `service_plan_id` de las apps, por lo que el plan aun tenia ambas apps cuando intento borrarlo.

### Estado del entorno: PARCIAL pero FUNCIONAL
- 0 recursos modificados en este apply: el destroy fallo primero y aborto los dos updates in-place.
  Ambas Function Apps siguen en el plan viejo y Running. No hay outage.
- Los dos planes nuevos ya existen (con 0 apps), creados por el apply local previo. Reintentar NO
  los duplicara (Terraform los vera en estado y los dejara igual).
- El unico paso pendiente es: reasociar las 2 apps a sus planes nuevos y luego destruir el viejo.
  Mientras el plan viejo tenga apps, su destroy seguira dando 409 en cada reintento. Reintentar el
  apply tal cual fallara identicamente de forma deterministica.

## Hipotesis (ordenadas por probabilidad)

### H1: orden del grafo destruye el plan compartido antes de reasociar las apps (confianza: ALTA)
- Evidencia: el log muestra el destroy ejecutado primero y el 409 11003 "has web app(s) ... assigned".
  El codigo elimina `module.service_plan` sin `create_before_destroy`. Azure confirma que el plan
  viejo sigue con 2 apps.
- Contra-evidencia: ninguna.
- Verificacion: ya verificada con log + estado en Azure.

### H2: colision con el apply local concurrente (confianza: BAJA)
- Evidencia: existio un apply local en paralelo (worktree #172); los 2 planes nuevos ya estan creados.
- Contra-evidencia: el log de CI adquiere y libera el lock sin error; el fallo es 409 de Azure, no
  lock de estado ni drift. El apply local solo dejo los planes nuevos creados; no es la causa del 409.
- Verificacion: revisar el log del apply local del harness si se quiere cerrar el cabo de "quien creo
  los planes nuevos", pero no cambia la causa raiz ni la remediacion.

## Acciones
Pendiente de validacion del usuario. Opciones de remediacion propuestas (NINGUNA ejecutada):

1. Reintento en dos fases con el codigo actual (sin tocar codigo):
   - Fase A: `terraform apply -target=module.function_app_control_horas
     -target=module.function_app_programacion` para reasociar ambas apps a los planes nuevos.
   - Fase B: `terraform apply` completo; el plan viejo ya quedara sin apps y su destroy procedera.
   - Requiere ejecutar `apply` (escritura): fuera del alcance de esta sesion de solo lectura.

2. Fix en codigo (issue): agregar `lifecycle { create_before_destroy = true }` no resuelve por si solo
   el caso "destruir un recurso eliminado de la config"; la opcion robusta es separar el cambio en dos
   PRs/applies (primero crear planes + reasociar apps; luego, en un segundo apply, eliminar el plac
   viejo de la config) o usar `-target` como en (1). Evaluar con infra-writer.

3. Issue propuesto: `bug, tipo:infra, dom:infra, estado:listo` -
   "Reasociar Function Apps a sus planes por dominio antes de destruir el plan compartido (#172)".

## Preguntas abiertas
- Quien lanzo el apply local en paralelo y si dejo el state remoto consistente con lo que ve Azure
  (los 2 planes nuevos en state). Conviene un `terraform plan` limpio para confirmar que el unico
  pendiente sean los 2 reassign + 1 destroy, sin drift adicional.
- Por que el grafo eligio destroy-first en este apply. AzureRM normalmente prioriza updates, pero al
  ser el plan viejo un recurso "removido de config" sin dependencia explicita hacia las apps tras el
  reapunte, el orden no esta garantizado. Documentar en el issue de fix.
