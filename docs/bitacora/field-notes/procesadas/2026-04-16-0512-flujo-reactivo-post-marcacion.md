---
fecha: 2026-04-16
hora: 05:12
sesion: event-stormer
tema: Flujo reactivo post-marcacion — de RegistroDeMarcacion a DiaCalculado
---

## Contexto
Sesion para disenar el flujo completo que ocurre despues de que una marcacion entra al sistema. El sistema es reactivo y sin aprobaciones intermedias — el unico lote de aprobacion es al momento de la transmision a nomina. Se partio de una propuesta inicial del usuario: RegistroDeMarcacion -> MarcacionRegistrada -> ControlDiario -> DiaCalculado.

## Descubrimientos

### RegistroDeMarcacionAggregateRoot
- Vive en ControlHoras (no en un bounded context separado).
- Stream ID determinista: `{EmpleadoId}:{TimestampCrudo}`. Sin DispositivoId — "Flash no existe".
- Tres responsabilidades: idempotencia por duplicado exacto (mismo stream ID = Marten rechaza), normalizacion (truncar segundos al minuto), emitir MarcacionRegistrada.
- La idempotencia por minuto normalizado (timestamps crudos diferentes, mismo minuto) la resuelve ControlDiario, no este aggregate. Evita lookups y consultas a proyecciones.
- MarcacionRegistrada es evento privado con handler interno (no sale a Service Bus).

### Normalizacion de marcaciones
- Regla por defecto: truncar segundos (floor al minuto). 08:09:59 -> 08:09:00.
- Configurable por empresa en el futuro para otros redondeos.
- Se persiste el timestamp crudo (idempotencia), se emite el normalizado en el evento.

### Algoritmo de depuracion: secuencial por rango
- Alternativa descartada: asignacion por proximidad temporal (requiere umbrales arbitrarios, no se autorrepara).
- Elegido: secuencial por rango. El corte entre franjas adyacentes es el punto medio del gap.
- Todas las marcaciones en el rango se ordenan cronologicamente: primera = entrada, ultima = salida, intermedias se descartan.
- Se analizo con 5 pasos de marcaciones llegando una a una para un turno partido (6AM-12PM, 2PM-6PM). El algoritmo se autorrepara: al llegar una marcacion tardia, recalcula primera/ultima y corrige el resultado.
- Documentado con casos en `docs/eda/aggregates/control-diario.yaml`.

### Ventana de traslape nocturno
- Problema: marcaciones de madrugada pueden pertenecer al dia operativo anterior (turno nocturno que cruza medianoche).
- Solucion: marcaciones entre 00:00 y HoraCorte se envian al dia calendario Y al dia anterior. Las demas solo al dia calendario.
- Default: 4AM. Configurable por empresa.
- Cada ControlDiario decide si la marcacion le es relevante segun sus franjas. Si no, la ignora.
- Alternativas descartadas: ExistsAsync al store para verificar si el dia anterior existe (sobrecarga innecesaria para el 95% de marcaciones que no son nocturnas).

### Comportamiento reactivo de ControlDiario
- Cualquier cambio de estado (MarcacionAdicionada o TurnoDiarioAsignado) dispara: depurador -> calculadora -> DiaCalculado.
- DiaCalculado se emite SIEMPRE, incluso con ceros, porque cada evento tiene contenido diferente (marcaciones depuradas cambian).
- Sin programacion: DiaCalculado con ceros, marcaciones depuradas vacias, programacion nula. Evidencia de actividad sin informacion completa.
- Con una sola marcacion: depuracion con entrada sin salida (anomala). Calculadora retorna ceros.

### DiaCalculado (evento publico)
- Sale a Service Bus. Es lo que nomina consume.
- Payload: InformacionEmpleado (completo), Fecha (DateOnly), MarcacionesDepuradas, DesgloseHoras, ProgramacionUsada (DetalleTurno o nulo).
- No incluye marcaciones crudas/sin depurar — solo las depuradas.

## Decisiones
- **Depuracion y CalculoHoras absorbidos por ControlHoras**: no son bounded contexts separados. Son logica interna de ControlDiarioAggregateRoot. -> candidato a ADR (actualizar ADR-0001 con los dominios definitivos)
- **DiaOperativo descartado**: el aggregate dia-operativo.yaml (con estados de depuracion, conciliacion, aprobacion) se elimina. ControlDiario lo reemplaza con un modelo reactivo sin estados intermedios.
- **Algoritmo secuencial por rango**: sobre proximidad temporal, por ser determinista, sin umbrales y autorreparable.
- **Ventana de traslape nocturno 4AM**: pragmatico para la mayoria de negocios sin turnos extremos.
- **MarcacionRegistrada como evento privado**: handler interno en ControlHoras. Se promueve a publico si surge necesidad.
- **Idempotencia por minuto normalizado en ControlDiario**: no en RegistroDeMarcacion. Evita lookups costosos.

## Descartado
- **DiaOperativo como aggregate** (modelo con estados Pendiente/Depurado/ConciliacionPendiente/Conciliado/Aprobado): demasiada ceremonia para un sistema reactivo sin aprobaciones intermedias.
- **Depuracion como bounded context**: no justifica un dominio separado. Es logica interna del aggregate.
- **CalculoHoras como bounded context**: idem. La calculadora es un metodo interno.
- **Proximidad temporal para depuracion**: requiere umbrales arbitrarios, genera ambiguedades con franjas completas que reciben marcaciones adicionales.
- **ExistsAsync para ruteo nocturno**: sobrecarga por marcacion solo para descartar el 95%.
- **Validacion temporal de marcaciones (futuro/pasado)**: descartada por complejidad de relojes desincronizados y zonas horarias. Sin valor inmediato.
- **Nivel 2 de idempotencia en RegistroDeMarcacion**: trasladado a ControlDiario para evitar lookups.

## Preguntas abiertas
- Como funciona el flujo de aprobacion y transmision a nomina (el unico lote de aprobaciones)
- Donde vive la configuracion de la Ventana de traslape nocturno por empresa
- Donde vive la configuracion de la regla de normalizacion/redondeo por empresa
- Como llegan las marcaciones del sistema externo a RegistroDeMarcacion (HTTP o Service Bus)
- Recepcion de lotes de Registros de Marcacion: como se extrapola el caso unitario

## Referencias
- ADRs consultados: ADR-0004 (topics por evento), ADR-0002 (contracts)
- Artefactos actualizados: context-map.yaml, ubiquitous-language.yaml, control-diario.yaml
- Artefactos eliminados: dia-operativo.yaml
