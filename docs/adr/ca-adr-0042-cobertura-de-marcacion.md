# CA-ADR-0042: Cobertura de marcacion

## Estado
Aceptado (2026-10-21). Reemplaza la regla de la Ventana de traslape nocturno (sesion 2026-04-16, sin ADR) como regla de pertenencia y, con #1125, de ruteo. Origen: #1113; implementado en #1124.

## Contexto
El depurador capturaba en la primera franja desde `DateTime.MinValue` y en la ultima hasta `DateTime.MaxValue`. Como el traslape nocturno envia toda marcacion de 00:00 a 04:00 al dia del reloj y tambien a la vispera, la salida de madrugada de un nocturno de la vispera se tomaba como entrada del turno del dia (retardo 0, tiempo trabajado desde la madrugada). Con nocturnos consecutivos el dia tomaba la salida de su propia fecha como entrada (~24 h). #1107 solo lo corrigio para turnos con ventana, con la hora de corte (04:00).

La hora de corte nacio como regla de ruteo pragmatica y la relevancia se delego al depurador, que nunca la implemento. Una hora fija de empresa falla con nocturnos que terminan despues del corte (22:00-06:00), 12x12 y turnos de 24 h.

## Decision
1. **Cobertura de marcacion** = [apertura - 2 h, fin efectivo + 4 h). Apertura = menor inicio de las franjas; fin = mayor fin resuelto (incluye `DiaOffsetFin`). Sin franjas: sin cobertura. El depurador solo usa marcaciones dentro de ella.
2. **Ancla de la ventana** = primera marcacion dentro de la cobertura posible del turno (la de referencia extendida en la amplitud de la ventana), sin las del solape con un vecino, acotada a la ventana (#1202).
3. **Reparto**: toda marcacion se guarda en su dia calendario y una reaccion a `DiaDepurado` la entrega a la vispera o al dia siguiente si la cobertura posible de su turno la contiene, y recoge de ellos las que le corresponden (#1125).
4. **Guardar y luego mirar**: el reparto lee a los vecinos en otra transaccion, posterior al commit del dia, para que ninguna marcacion se pierda en una carrera marcacion/turno.
5. Una marcacion en la cobertura de dos turnos es **ambigua**: ninguno la usa, la decide el Aprobador (#1127, #1130).
6. Programacion advierte descansos menores a 6 h (#1128).
7. La hora de corte se retira (#1125, #1129).

## Consecuencias
- No depende de una hora de empresa (12x12, 24 h, nocturnos hasta las 06:00).
- Una salida despues de fin + 4 h queda fuera (dia anomalo); las marcaciones fuera de cobertura se conservan en el stream y en `DiaDepurado.Marcaciones`.
- Los margenes son constantes privadas de `TurnoDiario` hasta que se configuren por empresa.
- Descansos < 6 h producen marcaciones ambiguas.

## Alternativas descartadas
- **Dia operativo de corte a corte por empresa**: una hora fija falla con nocturnos largos, 12x12 y 24 h.
- **Frontera por punto medio entre turnos vecinos**: depende de turnos ajenos y no define borde con un solo turno.
- **Linea de tiempo por colaborador**: acopla el calculo del dia a la programacion de otros dias.
- **Envio ciego a la vispera con horizonte**: reintroduce el defecto de tomar marcaciones ajenas como entrada.
- **Que ambos dias usen la marcacion del solape**: duplica la marcacion y produce horas dobles.

## Control de cambios

- 2026-10-30: la decision 4 se precisa para el camino en lote por CA-ADR-0043 (el camino por dia la conserva).
