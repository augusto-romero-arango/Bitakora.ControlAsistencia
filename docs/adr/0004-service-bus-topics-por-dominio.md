# ADR-0004: Service Bus - un topic por dominio productor

## Estado

Aceptado

## Contexto

El principio central de la arquitectura es que "la verdad viaja en el evento": cada dominio
es el unico productor de los eventos que le pertenecen y ninguno puede modificar la verdad
de otro dominio directamente.

Para implementar este principio con Azure Service Bus existen dos topologias principales:

1. **Queues por consumidor**: cada consumidor tiene su propia cola y el productor envia a
   cada cola. Esto acopla al productor con sus consumidores y obliga a actualizar el
   productor cada vez que se agrega un nuevo consumidor.

2. **Topics con subscriptions**: el productor publica a un topic y cada consumidor tiene su
   propia subscription. El productor no necesita saber quienes lo consumen.

La opcion 1 viola el principio de autonomia: el productor de marcaciones tendria que
conocer a todos los dominios que quieren escuchar sus eventos. Agregar un nuevo consumidor
requeriria modificar el productor.

## Decision

Se usa un topic de Service Bus por dominio productor. El naming de los recursos sigue la
convencion definida en ADR-0005:

- Topics: `eventos-{dominio-en-kebab}` (e.g. `eventos-marcaciones`, `eventos-empleados`)
- Subscriptions: `{consumidor}-escucha-{productor}` (e.g. `liquidacion-escucha-marcaciones`)

Cada dominio publica **unicamente** a su propio topic. Ningun dominio publica al topic de
otro dominio.

Los topics y subscriptions se gestionan como infraestructura mediante Terraform (ver
estructura en `infra/`). La topologia completa de que dominio escucha a quien se define
con el agente `eda-modeler`.

## Consecuencias

**Positivas**

- Cada consumidor tiene su propio cursor de lectura y su propio dead-letter queue:
  un consumidor puede estar caido sin que los mensajes se pierdan ni bloqueen a otros.
- Aislamiento de consumidores: un consumidor lento o con errores no afecta el throughput
  de los demas consumidores del mismo topic.
- Agregar un nuevo consumidor solo requiere crear una nueva subscription en Terraform,
  sin tocar el productor.

**Negativas**

- Mas recursos en Azure: N topics mas M subscriptions en lugar de un conjunto simple de
  colas. El costo incremental en Service Bus Standard es bajo pero existe.
- La topologia completa (que subscriptions existen) vive en Terraform y no es inmediatamente
  visible desde el codigo de la aplicacion.
