# CA-ADR-0025: El modelo de dominio rico no cruza el bus

## Estado

Aceptado

## Contexto

El calculo de horas de ControlHoras se apoya en un modelo de dominio rico: value objects con
invariantes, comportamiento (Tell-don't-Ask) y estado interno encapsulado -- `IntervaloTemporal`,
`MomentoDelDia`, `IntervaloClasificado`, `DesgloseFranja`, `DesgloseHoras`, `Retardo` y el enum
`Concepto`. Varios de estos tipos tienen constructor privado + campos `readonly` y dependen del
resolver custom de Marten (`ConfiguracionSerializacionControlHoras`) para deserializarse.

Historicamente estos tipos vivian en `Contracts` porque el evento publico `DiaCalculado` cargaba el
modelo rico como payload. Eso resulto ser un error: el canal de publicacion a Service Bus usa el
serializador POR DEFECTO (sin el resolver custom), de modo que los tipos con ctor privado/campos
privados se serializaban de forma lossy hacia los consumidores externos (sistema de nomina). El bug
quedo documentado en las field notes del 2026-06-23.

`#183` curo el bug aplanando el payload: `DiaCalculado` ahora viaja con `HorasDiscriminadas`, un
record 100% primitivo (`IReadOnlyDictionary<string,int>` + `IReadOnlyList<string>`) que STJ
serializa nativamente sin resolver custom. `#184` poblo su trazabilidad. Tras ambos, **ningun
`IPublicEvent` referencia ya el modelo rico**: solo lo consume el propio dominio.

Quedaba la deuda de ubicacion: tipos de dominio interno alojados en `Contracts`, el proyecto que
CA-ADR-0002 reserva para el vocabulario que cruza entre dominios.

## Decision

**El modelo de dominio rico vive en su dominio (ControlHoras) y nunca cruza el bus. Solo los DTOs
planos viajan en eventos publicos.**

1. Los value objects ricos (`IntervaloTemporal`, `MomentoDelDia`, `IntervaloClasificado`,
   `DesgloseFranja`, `DesgloseHoras`, `Retardo`) y el enum `Concepto` viven en
   `Bitakora.ControlAsistencia.ControlHoras.ValueObjects`, no en `Contracts`.
2. `Contracts` conserva unicamente DTOs/eventos planos. Para ControlHoras eso es `HorasDiscriminadas`
   (payload de `DiaCalculado`), serializable con STJ por defecto sin ningun `ConfigurarSerializacion`.
3. `Concepto` se trata como parte del modelo rico (no del contrato): la clave estable que viaja en el
   diccionario plano es `Concepto.ToString()` (un `string`), no el enum. Por eso `Concepto` puede y
   debe vivir en el dominio sin que ningun consumidor externo dependa de el.
4. El resolver custom de Marten (`ConfiguracionSerializacionControlHoras`) sigue registrando estos
   tipos: aplica solo al **event store interno** (persistencia/rehidratacion del aggregate), nunca al
   canal de publicacion. Esa separacion es justamente lo que vuelve el bug de payload lossy
   estructuralmente imposible.

Regla operativa para futuras decisiones: si un tipo tiene invariantes, ctor privado o campos privados
y necesita el resolver custom, NO puede ser payload de un `IPublicEvent`; traduzcase a un DTO plano
antes de publicar (patron `Discriminar()` de `DesgloseHoras`).

## Consecuencias

- `Contracts` deja de depender del resolver custom de Marten para el calculo de horas; su superficie
  publica es 100% primitiva y serializable por defecto.
- Un desarrollador no puede "arreglar" un evento agregandole un tipo rico sin romper esta regla; la
  barrera anti-regresion ya existe en los tests (round-trip de `HorasDiscriminadas` con el resolver
  por defecto debe pasar; los tipos ricos fallarian con `NotSupportedException`).
- `DetalleRetardo` se renombro a `Retardo` (termino del glosario) al salir de `Contracts`: ya no
  necesita el prefijo `Detalle` que sugeria un DTO de contrato.
- Refuerza CA-ADR-0002 (Contracts = vocabulario cross-domain plano) y convive con el ADR de
  serializacion (value objects con ctor privado + `ConfigurarSerializacion`), que ahora se entiende
  como mecanismo exclusivo del event store interno.
