# ADR-0020: Versión de .NET y plan de hosting Azure Functions

## Estado

Aceptado

## Contexto

El proyecto usa Azure Functions isolated worker como plataforma de despliegue (ver ADR-0001). Al migrar a .NET 10 en abril 2026 se descubrió que **el plan Consumption Y1 no soporta .NET 10**: la Function App responde con 503 y nunca arranca porque las imágenes de contenedor de Y1 no incluyen el runtime .NET 10.

Los diagnósticos iniciales apuntaron incorrectamente al código (errores tipo "malformed content", "sync trigger failed") cuando el problema real era combinado: plan de hosting inadecuado, variables de entorno faltantes y flags de publish incorrectos. Sin una decisión documentada, cada nuevo dominio corre riesgo de replicar el error.

Además, el usuario quiere mantener la versión más reciente de .NET por razones de soporte, rendimiento y características del lenguaje. Hacer downgrade de .NET para acomodar limitaciones de Azure no es una opción aceptable.

## Decisión

### Versión de .NET

- El proyecto usa siempre **la versión más reciente estable de .NET**.
- **No se hace downgrade de .NET** para resolver incompatibilidades con servicios Azure. Si un servicio no soporta la versión actual, se cambia el servicio o el plan, no la versión.

### Plan de hosting

- **Plan mínimo: B1 (Basic dedicado)**. Validado en producción y en el repo gemelo `Cosmos-SincoERP/ControlPlane` (B3).
- **Prohibido Consumption Y1** mientras el proyecto use una versión de .NET que Y1 no soporte nativamente. Flex Consumption o Premium (EP1) son alternativas aceptables si se necesita escalado serverless.
- El default del módulo `infra/modules/service-plan` es `B1`. Subir el SKU requiere justificación documentada en el issue de infra.

### App settings obligatorios para toda Function App

Los siguientes settings deben existir en el módulo Terraform `infra/modules/function-app`:

```
FUNCTIONS_WORKER_RUNTIME               = "dotnet-isolated"
FUNCTIONS_EXTENSION_VERSION            = "~4"
WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED = "1"
WEBSITE_RUN_FROM_PACKAGE               = "1"
```

Omitir cualquiera de estos provoca arranques fallidos o degradación silenciosa.

### Comandos de publish en GitHub Actions

El workflow de cada dominio debe invocar dotnet con estos flags:

```bash
dotnet restore -r linux-x64
dotnet build   --configuration Release --no-restore -r linux-x64
dotnet publish --configuration Release --no-build   -r linux-x64 --self-contained false --output ./publish
```

Sin `-r linux-x64 --self-contained false` el artefacto no corre correctamente en el host Linux de Azure Functions. El `domain-scaffolder` ya genera los workflows con estos flags.

## Consecuencias

**Positivas**
- Decisión documentada y reproducible: cualquier nuevo dominio hereda la configuración correcta automáticamente (defaults de los módulos Terraform + scaffolder).
- Diagnóstico acelerado ante fallos de deploy: la checklist del `bug-investigator` apunta directo a estos puntos.
- Coherencia con el repo gemelo `Cosmos-SincoERP/ControlPlane`.

**Negativas**
- Costo base mayor que Consumption Y1 (B1 ~ $13 USD/mes por Function App activa vs. pago por uso de Y1).
- Mitigación: el módulo service-plan permite compartir un mismo plan entre varios dominios en `dev` para reducir costo fijo. En `prod` se evalúa por dominio.

**Riesgo aceptado**
- Si Microsoft extiende soporte de .NET 10 a Y1 en el futuro, esta decisión puede relajarse. Requerirá un nuevo ADR que la supersede.

## Referencias

- ADR-0001: Function App por dominio
- Módulo Terraform: `infra/modules/service-plan/main.tf` (SKU default B1)
- Módulo Terraform: `infra/modules/function-app/main.tf` (app_settings)
- Agente `domain-scaffolder`: genera workflows con los flags correctos
- Repo gemelo validado: `Cosmos-SincoERP/ControlPlane` (B3 + .NET 10)
