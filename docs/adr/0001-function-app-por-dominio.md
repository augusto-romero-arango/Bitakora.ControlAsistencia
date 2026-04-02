# ADR-0001: Function App por dominio

## Estado

Aceptado

## Contexto

El sistema maneja aproximadamente 50.000 empleados distribuidos en multiples empresas. Esta
compuesto por 4 dominios con perfiles de carga muy distintos entre si. El dominio de
marcaciones experimenta picos brutales entre las 6 y las 8 de la manana, cuando los empleados
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

Estructura de proyectos resultante:

```
src/
  Bitakora.ControlAsistencia.Marcaciones/      -- Function App de marcaciones
  Bitakora.ControlAsistencia.Empleados/        -- Function App de empleados
  Bitakora.ControlAsistencia.Liquidacion/      -- Function App de liquidacion
  Bitakora.ControlAsistencia.Notificaciones/   -- Function App de notificaciones
```

## Consecuencias

**Positivas**

- Despliegue independiente por dominio: un cambio en empleados no requiere redesplegar
  marcaciones.
- Escalado independiente en Consumption Plan: cada Function App escala segun su propia
  demanda, lo que permite absorber los picos de marcaciones sin sobredimensionar los demas
  dominios.
- Aislamiento de fallos: un error en liquidacion no interrumpe el registro de marcaciones.

**Negativas**

- Un proyecto `.csproj` adicional por dominio aumenta la cantidad de artefactos a mantener
  y despliega en la solucion.
