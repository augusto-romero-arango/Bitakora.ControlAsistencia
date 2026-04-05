---
fecha: 2026-04-05
hora: 06:33
sesion: event-stormer
tema: Diseno del evento TurnoCreado y modelo de franjas para el dominio Programacion
---

## Contexto
Primera sesion de diseno de un evento de dominio real. El objetivo era disenar el comando CrearTurno, el evento TurnoCreado y los value objects asociados (franjas, descansos, extras) para el aggregate CatalogoTurnos del dominio Programacion.

## Descubrimientos

### Modelo de franjas con contencion
- Un Turno se compone de Franjas Ordinarias (de trabajo)
- Cada Franja Ordinaria puede contener Franjas de Descanso y/o Franjas de Extra
- Descansos y Extras son hojas — no pueden contener otras franjas
- Una Franja Ordinaria NO puede contener otra Ordinaria
- Descansos y Extras no pueden solaparse entre si dentro de la misma Ordinaria
- Todas comparten estructura base: horaInicio, horaFin, con calculo de duracion en minutos y conversores (horas, horas/minutos, horas decimales)

### Extras programadas vs no programadas
- Las extras en el turno representan un compromiso del empleado ("llegas a las 6AM, sabes que trabajas extras")
- Las extras no programadas (el jefe te pidio quedarte) se resuelven en Depuracion/CalculoHoras, no en Programacion
- Descubrimiento de dominio: "extra programada" es vocabulario nuevo

### Semantica temporal: inicio inclusivo, fin exclusivo
- Una franja de 8:00 a 11:00 = exactamente 180 minutos
- Las 11:00 NO se cuentan, la franja termina a las 10:59:59
- Esto permite franjas contiguas sin ambiguedad: si una termina a las 12:00 y otra empieza a las 12:00, no hay solapamiento

### Turnos que cruzan medianoche: offset +1, sin hora de corte
- Se descarto la idea de "hora de corte" (que el dia empiece a las 6AM en vez de medianoche)
- En su lugar, se usa un offset de dia relativo: +0 (mismo dia, implicito) y +1 (dia siguiente, explicito)
- Ejemplo: FranjaOrdinaria (22:00 -> 06:00 +1), FranjaDescanso (01:00 +1 -> 01:30 +1)
- El turno se asigna al dia en que inicia. Las marcaciones continuas pertenecen a esa asignacion
- La particion legal por medianoche (dominicales/festivos) es responsabilidad de CalculoHoras, no de Programacion
- Fuente: la legislacion colombiana SI parte las horas en medianoche para recargos dominicales (ej: sabado 10PM a domingo 3AM = recargo nocturno hasta 12AM + recargo nocturno dominical desde 12AM)

### Comando plano, evento rico
- El comando es plano: tres arreglos separados (ordinarias, descansos, extras) de tuplas (inicio, fin)
- No hay campo "tipo" — el tipo es la propiedad donde vive la tupla
- Las tuplas NO se reordenan — se respeta lo que envia el front
- El factory del evento detecta cuando fin < inicio e infiere el +1
- El factory construye el arbol de contencion (que descanso va dentro de que ordinaria)

### Identidad generada por el cliente
- El turnoId (Guid) lo genera el front, no el backend
- Permite seguimiento asincrono inmediato (ej: SignalR) sin esperar respuesta

### Nombre del turno
- No es unico, no es llave — es solo una etiqueta de catalogo
- No puede estar vacio
- ToString() genera representacion legible: "{nombre} (06:00-12:00)(14:00-16:00)"

### Errores descriptivos acumulados
- El factory acumula TODOS los errores de validacion antes de lanzar una sola excepcion
- Los mensajes deben guiar al usuario para la correccion
- Ejemplo: "El descanso (10:00-10:15) no esta contenido en ninguna franja ordinaria. Las franjas ordinarias son: (06:00-12:00), (14:00-16:00)"

## Decisiones

1. **Modelo de contencion Ordinaria -> Descanso/Extra**: las franjas de descanso y extra son hijas de las ordinarias, no pares. Resuelve la ambiguedad del glosario anterior donde Franja podia ser "de trabajo o de descanso".

2. **Offset +1 sin hora de corte**: la representacion temporal usa dia relativo (+0 implicito, +1 explicito). Se descarto la hora de corte porque la particion legal es de otro dominio.

3. **Comando plano, factory rico**: el comando es facil de construir desde el front (tres arreglos de tuplas). Toda la logica de construccion, inferencia de +1 y validacion vive en el factory del evento.

4. **Id generado por el cliente**: el front genera el Guid para permitir seguimiento asincrono.

5. **Errores acumulados**: el factory reporta todos los problemas de una vez, no falla en el primero.

6. **Distribucion de validaciones**:
   - Endpoint HTTP: JSON parseable, campos requeridos, tipos correctos, nombre no vacio
   - Command handler: idempotencia (ya existe turno con ese Id?)
   - Factory del evento: toda la logica de dominio

## Descartado

- **Hora de corte por turno**: se propuso que cada turno definiera cuando empieza su "dia" (ej: 6AM). Se descarto porque agrega complejidad innecesaria — la particion por medianoche para recargos es responsabilidad de CalculoHoras segun la legislacion colombiana.

- **Reordenamiento de tuplas en el command handler**: se propuso ordenar timestamps desordenados. Se descarto — el front envia (inicio, fin) tal cual y el factory infiere +1 cuando fin < inicio.

- **Campo "tipo" en las tuplas del comando**: se propuso un campo tipo:"ordinaria"|"descanso"|"extra" en cada tupla. Se descarto — el tipo es implicito en la propiedad del arreglo donde vive (ordinarias[], descansos[], extras[]).

- **Nombre unico del turno**: no es llave, no necesita ser unico. La identidad es el Guid.

## Preguntas abiertas

- Como se implementa el patron de acumulacion de errores en el factory? (lista de ValidationError vs AggregateException vs FluentValidation?)
- Necesitamos un limite maximo de franjas por turno?
- El turno debe validar duracion maxima legal o eso es de otro dominio?
- Como se modela el ToString() "{nombre} (franjas...)" — es un metodo del aggregate o del value object Turno?

## Referencias
- ADR-0002: Contracts, eventos y value objects (la verdad viaja en el evento)
- ADR-0005: Convencion de naming de eventos (TurnoCreado en participio pasado)
- ADR-0006: Event Sourcing con Marten y Wolverine
- ADR-0012: Patron de mensajes con .resx per-aggregate
- Legislacion: reforma laboral colombiana, jornada nocturna desde 7PM (Ley 1846/2017 + reforma 2025)
