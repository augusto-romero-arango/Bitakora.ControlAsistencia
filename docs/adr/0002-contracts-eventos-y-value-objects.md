# ADR-0002: Proyecto Contracts para eventos y value objects compartidos

## Estado

Aceptado

## Contexto

Con dominios autonomos que se comunican exclusivamente por eventos, surge la necesidad de un
vocabulario comun. Cuando el dominio de marcaciones publica un evento `MarcacionesRegistradas`
y el dominio de liquidacion lo consume, ambos deben coincidir en la forma exacta del mensaje:
que campos tiene, que tipos son, como se llaman.

Sin un mecanismo centralizado, cada dominio definiria su propia version del evento, lo que
deriva en duplicacion, desincronizacion y errores silenciosos en tiempo de ejecucion.

La solucion mas simple seria que los dominios se referencien entre si para compartir los
tipos, pero eso crea acoplamiento directo entre proyectos de dominio, violando el principio
de autonomia.

## Decision

Se crea un proyecto `Bitakora.ControlAsistencia.Contracts` de tipo classlib que actua como
vocabulario comun del sistema. Este proyecto contiene unicamente:

- **Records de eventos**: representan hechos ocurridos en el sistema (e.g.
  `MarcacionesRegistradas`, `HorasCalculadas`, `EmpleadoActualizado`).
- **Value objects compartidos**: tipos que tienen identidad o semantica propia y que varios
  dominios necesitan (e.g. `EmpleadoId`, `EmpresaId`, `PeriodoLaboral`).

El proyecto Contracts **no contiene** servicios, helpers, logica de negocio ni dependencias
a frameworks de aplicacion. Es una libreria de datos pura.

Todos los proyectos de dominio que necesiten publicar o consumir eventos referencian
unicamente este proyecto.

## Consecuencias

**Positivas**

- Contratos tipados verificados en tiempo de compilacion: si un productor cambia un campo
  requerido, todos los consumidores fallan al compilar, no en produccion.
- Fuente unica de verdad para el esquema de los eventos: no hay versiones divergentes del
  mismo contrato en distintos dominios.
- El proyecto Contracts es liviano y sin logica: facil de mantener y de auditar.

**Negativas**

- Cambios breaking en un contrato (renombrar un campo, cambiar un tipo) requieren coordinar
  la actualizacion de todos los consumidores antes de desplegar. Ver ADR-0005 para la
  estrategia de versionado que mitiga este riesgo.
