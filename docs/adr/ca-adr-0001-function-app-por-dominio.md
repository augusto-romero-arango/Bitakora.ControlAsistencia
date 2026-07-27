# CA-ADR-0001: Function App por dominio

## Estado

Aceptado (actualizado 2026-04-15: dominios definitivos)

## Contexto

El sistema maneja aproximadamente 50.000 empleados distribuidos en multiples empresas. Esta
compuesto por dominios con perfiles de carga muy distintos entre si. El dominio de control de
horas experimenta picos brutales entre las 6 y las 8 de la manana, cuando los empleados
inician jornada de forma masiva y simultanea. El dominio de empleados, en cambio, es
practicamente un CRUD de baja frecuencia donde los cambios ocurren de manera espaciada a lo
largo del dia.

La arquitectura base es serverless sobre Azure Functions y los dominios se comunican entre si
por eventos a traves de Azure Service Bus, sin llamadas directas entre ellos.

Agrupar todos los dominios en un solo proyecto y una sola Function App implicaria que el
escalado se aplica de manera uniforme a toda la aplicacion, ignorando las diferencias de carga.
Ademas, un fallo o un despliegue mal hecho en un dominio podria afectar a los demas.

## Decision

Se crea un proyecto `.csproj` de tipo isolated worker por dominio. Cada proyecto produce un
artefacto independiente que se despliega en su propia Azure Function App. Los dominios nunca
se llaman entre si de forma directa: toda comunicacion ocurre mediante la publicacion y
consumo de eventos en Service Bus.

### Dominios del sistema

Los dominios han evolucionado desde la concepcion inicial (Marcaciones, Empleados, Liquidacion,
Notificaciones) hasta los definitivos, producto de sesiones de knowledge crunching:

| Dominio | Responsabilidad | Estado |
|---------|----------------|--------|
| **Programacion** | Catalogo de turnos, asignacion de turnos a empleados, ciclos | En desarrollo |
| **ControlHoras** | Recepcion de marcaciones, depuracion, calculo de horas, emision de DiaCalculado | En desarrollo |
| **Empleados** | Registro maestro de empleados | Planificado |

**Dominios descartados durante el diseno:**

- **Marcaciones**: absorbido por ControlHoras. El registro de marcaciones es un aggregate
  (RegistroDeMarcacionAggregateRoot) dentro de ControlHoras, no un dominio separado.
- **Depuracion**: absorbido por ControlHoras. La depuracion es logica interna de
  ControlDiarioAggregateRoot (metodo depurador), no un bounded context.
- **CalculoHoras**: absorbido por ControlHoras. El calculo de horas es logica interna de
  ControlDiarioAggregateRoot (metodo calculadora).
- **Liquidacion**: renombrado a ControlHoras con alcance redefinido.
- **Notificaciones**: no se ha identificado como dominio necesario hasta ahora.

Estructura de proyectos resultante:

```
src/
  Bitakora.ControlAsistencia.Programacion/      -- Function App de programacion
  Bitakora.ControlAsistencia.ControlHoras/       -- Function App de control de horas
  Bitakora.ControlAsistencia.Empleados/          -- Function App de empleados
```

## Consecuencias

**Positivas**

- Despliegue independiente por dominio: un cambio en empleados no requiere redesplegar
  control de horas.
- Escalado independiente en Consumption Plan: cada Function App escala segun su propia
  demanda, lo que permite absorber los picos de marcaciones sin sobredimensionar los demas
  dominios.
- Aislamiento de fallos: un error en programacion no interrumpe el registro de marcaciones.
- Tres dominios en vez de los cuatro o cinco originales: menos artefactos de infraestructura
  sin sacrificar separacion de responsabilidades.

- Cada Function App tiene su propia Storage Account dedicada. Los nombres de Storage Account
  son globalmente unicos en Azure, por lo que se agrega un sufijo aleatorio de 6 caracteres
  generado con el recurso `random_string` de Terraform para garantizar unicidad. Esta decision
  sigue la recomendacion de "Beginning Azure Functions", capitulo 8 (Best Practices), que
  indica que compartir una Storage Account entre Function Apps crea contension de IOPS,
  afecta el escalado independiente y amplia el blast radius ante fallos de storage.

**Negativas**

- Un proyecto `.csproj` adicional y una Storage Account adicional por dominio aumentan la
  cantidad de artefactos a mantener en la solucion.
