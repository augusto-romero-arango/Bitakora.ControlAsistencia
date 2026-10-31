# CA-ADR-0043: Lote semantico de programacion por colaborador

## Estado

Aceptado (investigacion de throughput de programacion masiva 2026-10-28/29; implementacion en #1335-#1341).

## Contexto

La programacion masiva viaja hoy un mensaje por dia. Corrida del 2026-10-24 01:51 UTC: 394 solicitudes -> 3.632
dias -> ~11.000 invocaciones (AsignarTurno, RepartirMarcaciones y RecibirDepuracion por dia) -> 13,5 min. Los
cuellos fueron Postgres B1ms (CPU 83-91 %, 113 errores 53300), `RecibirDepuracionCuandoDiaDepurado` serial
(~600 msg/min, `maxConcurrentCalls: 1`) y el plan B1 de ControlHoras (CPU 55-95 %).

**Meta**: 130 colaboradores x 1 mes en <= 1 min, desde la solicitud hasta la proyeccion. *(Actualizada el
2026-10-31: la meta medible es 100 colaboradores x 1 mes en <= 1 min, el SLI de programacion masiva de
CA-ADR-0044.)*

A diferencia de CA-ADR-0036 (dominio, sin identificadores), este es un ADR **de transporte**, como CA-ADR-0017:
fija los nombres de los dos eventos privados y sus topics, porque #1336 (infra) y #1337 los consumen.

## Decision

1. **Lote semantico.** Un mensaje por solicitud (un colaborador x un turno x sus fechas libres) en vez de uno por
   dia. El lote es un concepto de transporte, no del dominio: no entra al glosario.
2. **El colaborador es el limite de consistencia.** Un lote modifica los K streams de `ControlDiario` de un
   colaborador en una transaccion. Se aparta de "un aggregate por transaccion" (Vaughn Vernon, *Effective
   Aggregate Design*, 2011, dddcommunity.org); mismo limite que CA-ADR-0036 decision 12.
3. **Reparto de marcaciones en memoria dentro del lote.** Desviacion de MEF-ADR-0024 decision #3 ("no hay atajo
   en memoria"). Solo los extremos del rango consultan vecinos fuera del lote (d-1 del primero, d+1 del ultimo).
4. **Precisa CA-ADR-0042 decision 4 solo en el lote.** Los dias del lote se reparten en la misma transaccion,
   porque la cola con sesion serializa al colaborador y elimina la carrera marcacion/turno. El camino por dia
   conserva "guardar y luego mirar".
5. **Cola compartida.** El lote entra por `actualizar-control-diario` como Subject nuevo (MEF-ADR-0026).
   `MartenEventStore` (`Cosmos.EventSourcing.CritterStack` 3.1.0) hace `Append` sin version esperada, asi que la
   serializacion por colaborador depende de esa cola. El productor publica con `GroupId = CodigoColaborador`.
6. **Depurados por el bus.** El lote de dias depurados viaja por Service Bus a la misma cola (respeta
   MEF-ADR-0024 #3 fuera del lote).
7. **Nombres.** Evento privado `ProgramacionTurnoEnFechasSolicitada` (Programacion -> ControlHoras), topic
   `programacion-turno-en-fechas-solicitada`; evento privado `DiasDepurados` (ControlHoras -> ControlHoras),
   topic `dias-depurados`.
8. **Granularidad.** La solicitud completa viaja en un mensaje. El lote de depurados se parte por tamano
   serializado, por debajo de los 256 KB de Service Bus Standard; el tope concreto se mide y se fija en #1338.
9. **Reintentos.** Un fallo reprocesa el lote completo y el envio es inline sin outbox (`SendInline`), asi que
   los handlers son idempotentes (exito sin evento, CA-ADR-0035).
10. **Camino paralelo.** El camino por dia (`POST programacion/solicitudes`, `ProgramacionTurnoDiarioSolicitada`,
    `DiaDepurado`) no cambia. El lote entra por una ruta propia, `POST programacion/solicitudes-en-fechas`
    (#1337); es una divergencia consciente de MEF-ADR-0043 paso 1 (create disfrazado), aceptada para que los dos
    caminos convivan y se comparen, y uno se retira cuando la medicion decida. El asistente elige camino con un
    interruptor (#1340).

## Alternativas consideradas

- **Un mensaje por dia (camino actual).** Descartada: ~11.000 invocaciones para 3.632 dias; no alcanza la meta.
- **Respetar "guardar y luego mirar" dentro del lote.** Descartada: duplica escrituras y pierde parte del ahorro.
- **Concurrencia optimista en el event store en vez de la cola.** Descartada: `MartenEventStore` 3.1.0 no expone
  version esperada.
- **Depurados en memoria hasta `DiaCalculado`.** Descartada: extiende la desviacion de MEF-ADR-0024 #3 mas alla
  del lote de un aggregate.
- **Lote como concepto del dominio.** Descartada: el glosario ya lo descarto (sesion de #589); el lote es un
  concepto de transporte.

## Consecuencias

### Positivas
- Menos invocaciones y menos escrituras por dia.
- El camino actual queda intacto y comparable con la prueba de carga (#1335, CA-ADR-0041).

### Negativas
- Una transaccion mas grande por colaborador (mas trabajo perdido por reintento).
- Dos desviaciones documentadas (MEF-ADR-0024 #3, CA-ADR-0042 #4).
- Un `DiaCalculado` puede recibir a la vez escrituras del camino por dia (subscription sin sesion) y del lote;
  riesgo que se evalua en #1339.

## Referencias

- Issues: #1333 (este ADR), #1335-#1341 (desglose).
- MEF-ADR-0024, MEF-ADR-0026, MEF-ADR-0043, MEF-ADR-0001, MEF-ADR-0005.
- CA-ADR-0017 (ADR de transporte), CA-ADR-0025, CA-ADR-0029, CA-ADR-0035, CA-ADR-0036, CA-ADR-0041, CA-ADR-0042.
- Glosario (`docs/ddd/ubiquitous-language.yaml`): "lote como concepto del dominio" descartado (sesion de #589).
- Field note `2026-10-23-1038`: carrera marcacion/turno entre topics que origino CA-ADR-0042 decision 4.
- Vaughn Vernon, *Effective Aggregate Design*, 2011, dddcommunity.org.

## Control de cambios

- 2026-10-30: creado (investigacion 2026-10-28/29; registro en #1333).
- 2026-10-31: la meta medible pasa a 100 colaboradores x 1 mes en <= 1 min (CA-ADR-0044, #1335).
