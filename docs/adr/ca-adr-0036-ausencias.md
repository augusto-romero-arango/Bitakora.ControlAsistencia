# CA-ADR-0036: Ausencias

## Estado

Aceptado (exploracion del planner 2026-10-02; implementacion en #742-#752).

## Contexto

El Turno es el plan de un dia (CA-ADR-0033) y la plantilla semanal lo compone en el tiempo (CA-ADR-0034). Falta
un concepto para el dia en que **no se espera que el colaborador trabaje** por una razon distinta al descanso
planeado: vacaciones, una incapacidad medica, una licencia. Hoy ese hecho solo se podria simular con un turno,
lo que mezcla "plan de trabajo" con "excepcion al plan" y pierde el motivo, que es lo que nomina necesita.

El 2026-10-02 el experto fijo el modelo con el planner. Este ADR registra **decisiones de dominio y de
arquitectura**, sin detalles de implementacion: los nombres de clases, aggregates, eventos, comandos y endpoints
se fijan al refinar cada issue del desglose (#742-#752).

## Decision

### Dominio

1. **Ausencia.** Es un dia completo en que no se espera que el colaborador trabaje, con un motivo. No es un
   turno ni pertenece al catalogo de turnos.
2. **Motivos, lista fija.** Vacaciones, incapacidad medica, licencia remunerada y ausencia no remunerada. El
   sistema entrega el Motivo de ausencia; nomina decide el pago y el conteo (dias habiles o calendario).
3. **Cubre el turno, no lo reemplaza.** Al cancelarla vuelve el turno planeado, o el dia queda sin programar si
   no lo habia. Turno sobre turno sigue siendo "el ultimo gana".
4. **Se protege.** Programar no la sobrescribe: se programan los dias libres y se informan los respetados. Una
   ausencia que choca con otra se rechaza completa. Los rechazos son decisiones de negocio sin consumidor que
   reaccione (CA-ADR-0030).
5. **El rango cubre todas las fechas**, incluidos descansos y dias sin programar.
6. **La registra el Programador y queda en firme.** No hay flujo de aprobacion.
7. **Se cancela por fechas.** Cancelarla entera es la misma operacion sobre todas sus fechas.
8. **Marcar en un dia de ausencia es una anomalia.** No se calculan horas.
9. **Lo aprobado no cambia.** Una ausencia o cancelacion que llega a un dia Aprobado queda registrada pero no
   lo mueve.
10. **Permiso** (ausencia por horas dentro de un turno) es un concepto distinto, reservado y fuera de alcance.

### Arquitectura

11. La ausencia se **registra en Programacion** y se **aplica dia a dia en ControlHoras**, por el mismo canal
    que la programacion de turnos.
12. **El limite de consistencia es el colaborador.** Sus ausencias se validan juntas, para que los choques
    (decisiones 4 y 7) se detecten transaccionalmente.
13. **Programacion no conoce los dias aprobados.** Se descarto replicarlos (MEF-ADR-0046, dato ajeno entre
    dominios); la decision 9 se cumple de forma diferida en ControlHoras, sin rechazo inmediato.

## Alternativas consideradas

- **Ausencia como turno del catalogo.** Descartada: mezcla el plan de trabajo con la excepcion al plan, obliga a
  reemplazar el turno (se pierde al cancelar) y no entrega un motivo inequivoco a nomina.
- **Ausencia que reemplaza el turno.** Descartada: al cancelarla el turno planeado ya no existe y habria que
  reconstruirlo; cubrirlo lo conserva intacto.
- **Replica de los dias aprobados en Programacion.** Descartada (MEF-ADR-0046): duplica un dato ajeno entre
  dominios y obliga a mantenerlo sincronizado; basta aplicar la regla en ControlHoras.
- **Flujo de solicitud y aprobacion.** Descartada: el experto fijo que la registra el Programador y queda en
  firme; la aprobacion agrega estados sin valor para el calculo.
- **Guardar soporte o diagnostico.** Descartada: es dato sensible y el calculo de horas no lo necesita.

## Consecuencias

### Positivas
- El turno planeado nunca se pierde.
- Las ausencias no se borran en silencio al reprogramar.
- Nomina recibe un motivo inequivoco.

### Negativas
- Una incapacidad durante vacaciones exige cancelar primero esas vacaciones (tratamiento legal por confirmar con
  nomina).
- El Programador no se entera en el momento si una ausencia cae en un dia Aprobado.

## Referencias

- Issues: #741 (este ADR), #742-#752 (desglose).
- MEF-ADR-0046 (dato ajeno entre dominios); CA-ADR-0030 (rechazos sin evento de fallo), CA-ADR-0033 (Turno),
  CA-ADR-0034 (plantilla semanal).
- Glosario (`docs/ddd/ubiquitous-language.yaml`): Ausencia, Motivo de ausencia y Permiso (llegan con el PR #734);
  Programador de turnos, Aprobado.
- Field note de la exploracion: `2026-10-02-1802-planner`.

## Control de cambios

- 2026-10-02: creado (exploracion del planner; registro en #741).
