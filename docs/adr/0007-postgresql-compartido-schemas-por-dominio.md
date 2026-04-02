# ADR-0007: PostgreSQL compartido con schemas separados por dominio

## Estado

Aceptado

## Contexto

Marten (el event store adoptado en ADR-0006) requiere una base de datos PostgreSQL. Hay que
decidir como organizar esa base de datos dado que el sistema tiene multiples dominios, cada
uno con su propia Function App.

Las opciones consideradas fueron:

1. **Un servidor PostgreSQL por dominio**: maxima autonomia, pero costo y complejidad
   operacional muy altos para la etapa actual del proyecto.
2. **Un servidor compartido, una base de datos por dominio**: cada dominio tiene su propia
   base de datos dentro del mismo servidor. Buen aislamiento, pero requiere gestionar
   multiples connection strings y bases de datos.
3. **Un servidor compartido, una base de datos, schemas separados por dominio**: un unico
   servidor y una unica base de datos, pero cada dominio opera en su propio schema de
   PostgreSQL. Marten soporta esto nativamente via el parametro de schema en la
   configuracion del event store.

## Decision

Se aprovisiona un unico `azurerm_postgresql_flexible_server` en la infraestructura base
del ambiente (no por dominio). Todos los dominios comparten el mismo servidor y la misma
base de datos (`controlasistencias`), pero cada uno opera en su propio schema de Marten.

### Configuracion del servidor

- **SKU**: `B_Standard_B1ms` (1 vCore, 2 GB RAM) - suficiente para desarrollo
- **Version**: PostgreSQL 17
- **Storage**: 32 GB
- **Collation**: `es_ES.utf8` (soporte correcto del espanol en texto y ordenamiento)
- **Firewall**: regla `0.0.0.0 - 0.0.0.0` para permitir acceso desde cualquier servicio Azure
- `lifecycle { prevent_destroy = true }` para proteger los datos ante cambios accidentales
  en Terraform

### Schemas por dominio

Cada Function App configura Marten con el nombre de su dominio como schema:

| Dominio | Schema de Marten |
|---------|------------------|
| Programacion | `programacion` |
| Marcaciones | `marcaciones` |
| CalculoHoras | `calculo_horas` |
| (etc.) | `{snake_case del dominio}` |

El nombre del schema usa la variante `snake_case` del nombre del dominio para seguir las
convenciones de nombrado de PostgreSQL.

### Connection string

La connection string de Marten se inyecta como app_setting `MartenConnectionString` en
cada Function App. Se construye en Terraform usando los outputs del modulo postgresql:

```
Host={server_fqdn};Database=controlasistencias;Username=pgadmin;Password={password};SSL Mode=Require
```

Para desarrollo local, `local.settings.json` apunta a un servidor PostgreSQL local:

```
Host=localhost;Database=controlasistencias;Username=postgres;Password=postgres
```

### Ubicacion en la infraestructura

El modulo `postgresql` se declara en la infraestructura base del ambiente (`main.tf`),
al mismo nivel que `monitoring` y `service_bus`. No se genera un servidor por dominio
cuando se usa el domain-scaffolder.

## Consecuencias

**Positivas**

- Un unico servidor minimiza el costo en la etapa de desarrollo.
- La administracion es mas simple: backups, actualizaciones de version y monitoreo aplican
  a un solo recurso.
- Marten gestiona la creacion y migracion de schemas automaticamente en el primer arranque
  de cada Function App, sin scripts manuales de DDL.
- Si en el futuro un dominio necesita su propio servidor (por carga o aislamiento de datos
  regulatorio), la migracion es transparente: solo cambia la connection string en los
  app settings de Terraform.

**Negativas**

- Todos los dominios comparten el mismo servidor: un pico de carga en un dominio puede
  afectar el rendimiento de los demas. Aceptable en dev, deberia reevaluarse en produccion
  para dominios de alta carga como Marcaciones.
- La contrasena del administrador de PostgreSQL es una variable de Terraform sensible
  (`postgresql_admin_password`) que debe proveerse via `TF_VAR_postgresql_admin_password`
  en el CI o en un `terraform.tfvars` excluido del control de versiones. No usar
  Key Vault references es deuda tecnica aceptada para esta etapa.
