# CA-ADR-0045: Borrador de programacion

## Estado

Aceptado (exploracion 2026-10-31; implementacion en #1405-#1412).

Enmienda CA-ADR-0043 (los dias sin publicar llegan a ControlHoras al publicar) y la decision 8 de CA-ADR-0044
(el SLI de programacion masiva se mide desde la publicacion).

## Contexto

Hoy programar es entrar en vigor: cada solicitud llega de inmediato a ControlHoras y las vistas de revision del
Programador leen lo vigente. El Programador de turnos necesita explorar como cubre vacantes moviendo la
programacion con libertad, sin que las propuestas intermedias afecten el calculo ni las vean (a futuro) los
colaboradores.

El turno y la plantilla (CA-ADR-0033, CA-ADR-0034) son moldes sin estados ni publicacion y sin efecto en el
calculo; la programacion si tiene ciclo de publicacion. El glosario rechazo "borrador" para el turno por esa
razon y rechazo un periodo persistido con estados (Ventana de trabajo, 2026-08-29): el borrador no persiste
periodos con estados, el mes es solo el corte de su historia (decision 14).

El 2026-10-31 el experto fijo el modelo con el planner. Este ADR registra **decisiones de dominio y de
arquitectura**, sin detalles de implementacion: los nombres se fijan al refinar cada issue del desglose
(#1405-#1412).

## Decision

### Dominio

1. **Borrador de programacion.** Es la programacion de los dias de un colaborador que nunca se han publicado.
   No rige para el calculo ni la ve el colaborador.
2. **Dia en firme.** Un dia queda en firme la primera vez que se le publica un turno. Es permanente. Un dia
   vacio dentro del rango publicado no queda en firme.
3. **Programar** (solicitud o aplicacion de plantilla) sobre un dia sin publicar escribe en el borrador; sobre
   un dia en firme se aplica directo, como hoy: es una modificacion de la programacion en firme, sin borrador ni
   nueva publicacion. Una solicitud que mezcla ambos se parte.
4. **Cancelar programacion.** Cancelar un dia que esta en el borrador lo quita del borrador; cualquier otro dia
   (en firme, o con programacion anterior a esta decision) se cancela directo, como hoy. El borrador solo
   contiene turnos.
5. **Publicar programacion** es el unico acto que pone el borrador en vigor. Camino unico: un dia nuevo no se
   programa en firme directamente; quien no quiera explorar publica enseguida (lo compone el cliente).
6. **Descartar borrador** quita los turnos del borrador; esos dias quedan sin programar.
7. **Alcances** de publicar y descartar: un colaborador, un grupo (sede o etiquetas) o todo lo pendiente. El
   Programador puede omitir el rango de fechas (sin rango = todo el borrador del alcance); como se resuelve lo
   fija la decision 16.
8. **Borrador vencido** (su fecha llego sin publicar): sigue pendiente y se marca; nada entra en vigor ni se
   descarta solo. Mientras tanto el dia esta sin programar para el calculo, o conserva la programacion anterior
   a esta decision si la tenia (decision 15).
9. **Lo aprobado no cambia.** Publicar sobre un dia Aprobado no lo mueve (CA-ADR-0036 decision 9).
10. **Las ausencias siguen en firme** y el borrador las respeta: programar sobre una Ausencia no la
    sobrescribe, como hoy.
11. **Lo que ve el Programador.** La malla semanal de turnos y las advertencias de la programacion semanal
    muestran como quedaria (dias en firme + dias en borrador) y marcan los dias en borrador; una vista de
    borradores pendientes muestra que falta publicar, incluidos los vencidos.
12. **Fuera de alcance:** notificar al colaborador o mostrarle su programacion. La publicacion es el hecho del
    que se colgara; despues de publicar, cada correccion en firme le seria visible al instante (aceptado por el
    experto).

### Arquitectura

13. **El borrador vive en Programacion.** ControlHoras no lo conoce: solo recibe la programacion al publicarse
    y las modificaciones en firme, por el mismo canal de hoy.
14. **El limite de consistencia es el colaborador en un mes calendario.** El borrador de cada colaborador se
    lleva por mes, compartido por todos los programadores, sin identidad del Programador. Ahi se sabe que dias
    de ese mes estan en firme. Programar, publicar y descartar actuan por fechas: si cruzan meses (la semana del
    28 de octubre al 3 de noviembre), tocan el borrador de cada mes. El corte acota la historia de cada
    borrador. Es mensual y no anual porque el cruce de periodos ocurre en cada programacion semanal y asi se
    ejercita siempre, no una vez al ano.
15. **Lo programado antes de esta decision no cuenta como en firme.** Sigue rigiendo para el calculo (ya esta en
    ControlHoras) y reprogramarlo pasa por el borrador, como cualquier dia sin publicar; al publicarse, lo
    reemplaza. No se reconstruye nada: ese conjunto queda congelado al adoptar la decision, porque desde
    entonces todo dia nuevo pasa por el borrador.
16. **Publicar y descartar reciben siempre un rango de fechas concreto.** "Sin rango" lo compone quien invoca
    el acto (el asistente, los alcances por grupo y en bloque), leyendo la vista de borradores pendientes, que
    reune todos los meses del colaborador. Es el mismo patron de la programacion por grupo: la composicion va
    fuera del acto y ningun acto de escritura lee la vista.

### Enmiendas

- **CA-ADR-0043:** para los dias sin publicar, la programacion llega a ControlHoras al publicar, no al
  solicitar. Los dias en firme no cambian.
- **CA-ADR-0044, decision 8:** el SLI de programacion masiva se mide desde la publicacion.

## Alternativas consideradas

- **Publicar al programar** como preferencia del tenant o bandera por solicitud. Descartada: reintroduce dos
  caminos (CA-ADR-0043 decision 10 acaba de retirar uno).
- **Borrador sobre dias ya publicados** (lo publicado rige debajo hasta republicar). Descartada: el borrador
  sirve para explorar antes de comunicar; despues, los cambios son correcciones en firme.
- **Borrador por Programador.** Descartada: exige identidad del Programador y resolver choques entre borradores.
- **Ausencias en borrador** (o segun el motivo). Descartada: son hechos, no exploracion; una incapacidad no
  puede esperar a publicarse.
- **Rango publicado completo en firme** (incluidos dias vacios). Descartada: al colaborador no se le comunico
  nada para esos dias.
- **Cancelacion en borrador.** Descartada: un dia sin publicar no tiene programacion en firme que cancelar.
- **Autopublicar o autodescartar lo vencido.** Descartada: nada entra en vigor ni se pierde sin un acto del
  Programador.
- **Exigir el rango al Programador / no admitir rango.** Descartada: el primero incomoda publicar todo; el
  segundo obliga a terminar todo antes de publicar algo.
- **Un borrador perpetuo por colaborador.** Descartada: su historia crece sin limite con cada mes programado, y
  acotarla despues exige compactar o archivar.
- **Borrador por ano.** Descartada: el cruce de periodos ocurriria una vez al ano y se perderia sensibilidad a
  esos casos.
- **Resolver "sin rango" dentro del acto.** Descartada: obliga a buscar en la persistencia que meses tienen
  pendientes, o a que un acto de escritura lea la vista, que llega con retraso.
- **Periodo de programacion persistido con estados.** Descartada: ya rechazado (Ventana de trabajo,
  2026-08-29).
- **Contar como en firme lo programado antes de esta decision** (reconstruirlo desde la historia de
  programacion). Descartada: solo sirve para un conjunto que se congela al adoptar la decision, y exige
  consultar la historia por colaborador sin indice, una proyeccion o una migracion. Lo programado antes sigue
  rigiendo y se reprograma por el borrador (decision del experto, refinamiento de #1421, 2026-11-02).

## Consecuencias

### Positivas
- El Programador explora sin efectos en el calculo.
- ControlHoras no cambia de lenguaje.
- La publicacion es el punto natural para notificar a futuro.
- Los dias en firme se corrigen igual que hoy.
- La historia de cada borrador queda acotada a un mes.

### Negativas
- Las vistas del Programador componen informacion de dos dominios (en firme en ControlHoras, borrador en
  Programacion).
- Reprogramar un dia con programacion anterior a esta decision no rige hasta publicar (mientras tanto rige la
  anterior).
- Un dia nuevo sin publicar queda sin programar para el calculo si el Programador olvida publicar (lo mitigan la
  vista de pendientes y la marca de vencidos).
- El asistente, los smoke tests y el benchmark de rendimiento dejan de ver efecto inmediato al programar dias
  nuevos y deben publicar.
- Un acto que cruza meses toca varios borradores.
- "Sin rango" depende de la vista, asi que un dia programado segundos antes puede quedarse en el borrador hasta
  la siguiente publicacion (no se pierde: sigue en los pendientes).

## Referencias

- Issues: #1404 (este ADR), #1405-#1412 (desglose).
- CA-ADR-0033 (turno), CA-ADR-0034 (plantilla), CA-ADR-0036 (ausencias; precedente de forma), CA-ADR-0043 (lote
  semantico, enmendado), CA-ADR-0044 (rendimiento, enmendado).
- Glosario (`docs/ddd/ubiquitous-language.yaml`): Borrador de programacion y Publicar programacion (llegan con
  el PR #1413); Ventana de trabajo; Turno incompleto; Ausencia; Aprobado.
- Corte por mes: Dudycz, [Keep your streams short](https://www.eventstore.com/blog/keep-your-streams-short-temporal-modelling-for-fast-reads-and-optimal-data-retention)
  y [Closing the books](https://event-driven.io/en/closing_the_books_in_practice/). Salidas de Marten para un
  stream que ya crecio: [compactar](https://martendb.io/events/compacting), [archivar](https://martendb.io/events/archiving).
- Field note de la exploracion: `2026-10-31-2133-planner`.

## Control de cambios

- 2026-10-31: creado (exploracion del planner; registro en #1404).
- 2026-11-02: decision 15 (lo programado antes no cuenta como en firme) segun el refinamiento de #1421.
