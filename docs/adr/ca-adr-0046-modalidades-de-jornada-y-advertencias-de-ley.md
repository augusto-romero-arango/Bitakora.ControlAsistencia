# CA-ADR-0046: Modalidades de jornada y advertencias de ley

## Estado

Aceptado (sesion planner 2026-11-01, refinada el 2026-11-07; implementacion en #1436-#1444).

Enmienda CA-ADR-0034 (la autoauditoria de la plantilla gana la capa de ley, decision 8) y el principio de la
Jornada ("no impone modalidades legales ni certifica cumplimiento", decision 4).

## Contexto

La investigacion #1290 (`docs/referencia/modalidades_jornada_colombia.md`) describe las modalidades legales de
jornada en Colombia y sus reglas medibles. El experto decide convertirla en ayuda al **Programador de turnos**,
empezando por advertencias. Hoy la Jornada solo tiene limites pactados y el `CalculadorAdvertencias` compara
contra esos valores exactos; no hay forma de advertir contra la ley.

Este ADR registra **decisiones de dominio y de arquitectura**, sin nombres de tipos nuevos: los fijan los issues
#1436-#1444 al refinarse. Sigue el precedente de ADR de dominio previo al desglose (#1404 -> CA-ADR-0045,
#741 -> CA-ADR-0036).

## Decision

### Dominio

1. **Dos capas de advertencias.** Contra lo pactado (la Jornada de hoy, valores exactos) y contra la ley
   (techos de la modalidad). Conviven y se distinguen en tipo y texto.
2. **Modalidad de jornada.** Atributo de la Jornada (no de la Vinculacion), de un catalogo cerrado que el sistema
   codifica (precedente: Festivo colombiano, ley codificada). El nombre viene de Mintrabajo (Concepto 115750/2026:
   "modalidades de jornadas laborales"). El CST no tiene termino generico: dice "jornada ordinaria" y llama
   "excepciones" a los literales del art. 161. La "modalidad" de las tools (CA-ADR-0039) es tecnica, no de
   dominio.
3. **Opcional.** Una Jornada sin modalidad es pactada libre: solo advertencias contra lo pactado y ninguna de ley
   (tampoco las transversales). Las Jornadas existentes quedan asi, sin migracion. Precedente: Jornada opcional de
   la plantilla (#867).
4. **Advierte, no certifica.** Sin advertencias se lee "sin advertencias contra la modalidad X", nunca "conforme a
   la ley". Nada bloquea. Enmienda el principio de la Jornada ("no impone modalidades legales ni certifica
   cumplimiento").
5. **Primer catalogo** (todas sus reglas caben en la semana lunes-domingo): Ordinaria (161 inc. 1), Flexible
   (161 lit. a), Turnos sucesivos (161 lit. d), Sin solucion de continuidad (166), Excluidos (162). Fuera, con ola
   propia: Menores (franja horaria y edad) y Turnos con promedio (165, periodo de hasta 3 semanas, #1444). Fuera
   sin ola: Insalubres (161 lit. b, sin cifras).
6. **Fichas de ley** por modalidad: las del documento #1290 seccion 3 (tope diario, minimo diario, techo semanal,
   descansos), con un ajuste en Ordinaria (decision 11). Reglas transversales con modalidad: semana con dia de
   descanso (172; con la marca de la decision 7, art. 184) y tope de extras programadas (167A). Ninguna modalidad
   del primer catalogo promedia entre semanas: cada semana se juzga sola (la Flexible promedia las horas diarias
   **dentro** de la semana).
7. **Descanso acumulado (art. 184).** Marca propia y opcional de la Jornada, independiente de la modalidad.
   Medicion: un dia de descanso por cada semana de trabajo seguido, tomados en la semana siguiente al fin de la
   racha (14x14 y 20x10 no advierten).
8. **Plantilla de N dias.** Los techos semanales de ley se juzgan sobre la **peor ventana circular de 7 dias** del
   ciclo, sin promedio. Con N no multiplo de 7 es exacta: toda ventana termina siendo una semana calendario al
   aplicar la plantilla (7 es primo). Con N multiplo de 7 (14-42) es conservadora: puede advertir sobre una
   ventana que, segun el dia de aplicacion, nunca es semana; se acepta por "advierte, no certifica". La semanal
   (N = 7, dia 1 = lunes) se juzga sobre el ciclo.

### Arquitectura

9. **Estampa.** Al programar, Programacion arma la ficha de ley vigente a la fecha del dia (p. ej. 42 h desde el
   15-jul-2026) y la copia en `JornadaProgramada` con la modalidad y la marca; ControlHoras solo compara y no
   duplica el catalogo (lo necesita tambien la plantilla, en Programacion). Con el borrador (CA-ADR-0045) la
   estampa ocurre al agregar al borrador y viaja al publicar. Costo aceptado: corregir el catalogo no cambia los
   dias ya estampados, igual que editar la Jornada hoy.
10. **Un solo calculador para dias programados.** Toda advertencia, pactada o de ley, sobre dias programados
    (programacion semanal y borrador, #1411) vive en `CalculadorAdvertencias`. La plantilla y la Jornada se
    auditan a si mismas en Programacion: juzgan su propio diseno, no dias programados.

### Dominio (refinamiento 2026-11-07)

11. **Ordinaria admite 5 dias** (refinamiento 2026-11-07). El tope diario de ley de la Ordinaria es el techo
    semanal vigente / 5 (8,4 h con 42 h), no las 8 h del inciso 1. Razon: repartir la jornada completa en 5 dias
    es practica comun (sobre todo administrativa) y el 161 inc. 1 autoriza distribuir la semana "en 5 o 6 dias";
    advertirla seria ruido en el caso mas frecuente. Se aparta de la letra del inciso 1 ("ocho (8) horas al dia")
    con respaldo solo de fuentes secundarias: **lectura marcada para validar con el abogado**.
12. **Unicidad** (refinamiento 2026-11-07). La identidad de una Jornada es limites + modalidad + marca de descanso
    acumulado. Dos Jornadas con los mismos limites y distinta modalidad o marca conviven (p. ej. 36/6 Ordinaria y
    36/6 Turnos sucesivos son distintas en descanso dominical y recargos). Asignar o quitar la modalidad (#1436) o
    la marca (#1437) que colisione con otra Jornada responde 409 nombrando la existente, como #890.

### Enmiendas

- **CA-ADR-0034:** la autoauditoria de la plantilla gana la capa de ley; los techos semanales de la modalidad se
  juzgan sobre la peor ventana circular de 7 dias del ciclo (decision 8).
- **Principio de la Jornada** ("no impone modalidades legales ni certifica cumplimiento"): la Jornada puede
  llevar una modalidad legal, y el sistema advierte contra ella sin certificar ni bloquear (decision 4).

### Contexto legal

- **6x2 de 8 h:** el cliente que lo programa opera sin poder detenerse, asi que le sirve Sin solucion de
  continuidad (166, hasta 56 h). El art. 166 exige demostrar la necesidad ante el inspector (Concepto 342949/2008);
  el sistema no lo verifica. Bajo Ordinaria advierte (alguna semana llega a 48 h). Su capa pactada (42 h exactas)
  seguira advirtiendo hasta #1444.
- Mintrabajo llama "jornada especial atipica" a los turnos 14x7 y 7x7, no regulados en el CST (Concepto
  115750/2026, via Accounter; fuente secundaria).
- **42 h en 5 dias:** el texto vigente del 161 (leyes.co) dice "ocho (8) horas al dia" y "42 horas a la semana ...
  en 5 o 6 dias"; su inc. 2 permite distribuir las horas diarias "de manera variable". Prensa (El Tiempo,
  Notaria 19) presenta 8,4 h en 5 dias como valido; ningun concepto de Mintrabajo revisado lo resuelve
  expresamente. El art. 164 (sabado libre) es el precedente historico pero conserva el texto de 48 h.
- La "semana" de la ley se asume lunes-domingo (no verificado, documento #1290 F1).
- Toda regla con efecto en el cliente se valida con un abogado laboralista (documento #1290, aviso inicial).

## Alternativas consideradas

- **Modalidad como preset de los 4 parametros** de la Jornada: descartada; la modalidad lleva reglas (descansos,
  extras, minimos) que no caben en los limites, y copiar valores pierde el vinculo con la ley.
- **Modalidad obligatoria u Ordinaria por defecto:** descartada; obligaria a migrar Jornadas existentes y
  atribuiria a la Jornada pactada libre una ley que nadie eligio.
- **Certificar o bloquear:** descartada; el catalogo tiene lecturas pendientes del abogado y requisitos que el
  sistema no verifica (166). Se advierte, no se certifica ni se bloquea.
- **Modalidad en la Vinculacion:** descartada; la Jornada ya se asigna a la Vinculacion y se estampa al
  programar, y las reglas del catalogo son de la labor y no de la persona.
- **"Regimen de jornada" y "Jornada especial":** descartados; se adopta el termino de Mintrabajo (decision 2).
  "Regimen" es el termino informal de `motor_horas_colombia_v2.md`, sin respaldo en la fuente oficial citada, y
  "jornada especial atipica" nombra para Mintrabajo los turnos 14x7 y 7x7 no regulados en el CST, lo que
  volveria ambiguo el nombre.
- **Advertir siempre sin marca de descanso acumulado:** descartada; 14x14 y 20x10 advertirian de forma
  permanente aunque el art. 184 los ampare.
- **Duplicar el calculo o el catalogo en ControlHoras:** descartada; la plantilla necesita el catalogo en
  Programacion y dos copias divergen (decisiones 9 y 10).
- **Promediar el ciclo de la plantilla:** descartada; ocultaria semanas que exceden el techo. Se juzga la peor
  ventana circular de 7 dias.
- **Bloques de 7 desde el dia 1 en la plantilla:** descartada; con N no multiplo de 7 la aplicacion desalinea
  los bloques y las semanas calendario reales difieren de los bloques juzgados.
- **Ordinaria con tope de 8 h y 5 dias por Flexible:** descartada; advertiria la jornada completa de 5 dias, el
  caso mas frecuente (decision 11).
- **Unicidad solo por limites:** descartada; 36/6 Ordinaria y 36/6 Turnos sucesivos son jornadas distintas
  (decision 12).

## Consecuencias

### Positivas
- El Programador recibe advertencias contra la ley sin perder la capa pactada actual.
- Las Jornadas existentes siguen igual: sin migracion ni advertencias nuevas.
- Un solo calculador y un solo catalogo (en Programacion) evitan divergencias entre dominios.
- La plantilla se audita contra la ley antes de aplicarse.

### Negativas
- Corregir el catalogo no cambia los dias ya estampados.
- La ventana circular puede advertir de mas con N multiplo de 7 (se acepta).
- La lectura de la Ordinaria en 5 dias (8,4 h) se aparta de la letra del 161 inc. 1 y queda por validar.
- La unicidad por tres componentes exige tocar los handlers que hoy comparan solo `LimitesJornada`
  (`CrearJornadaCommandHandler`, `ModificarLimitesJornadaCommandHandler`).
- Las reglas dependen de una semana lunes-domingo no verificada.

## Fuera de alcance

- Dia de descanso pactado (179 par. 3), compensatorio (183) y habitualidad por mes calendario (179 par. 1).
- Tramite ante el inspector (166), autorizacion del menor y orden del Gobierno (161 lit. b).
- Art. 164 (ampliacion por sabado libre) como modalidad propia.
- Pausa dentro de la jornada (167).
- Dos turnos el mismo dia (161 par. 1): se cumple por construccion, no requiere advertencia.
  `ControlDiarioAggregateRoot` es uno por colaborador y fecha con un solo `DetalleTurno` (un turno con varias
  franjas es un turno partido, no dos turnos).
- Menores y Turnos con promedio (165): olas propias (Turnos con promedio en #1444). Insalubres: sin ola.

## Referencias

- Issues: #1435 (este ADR), #1436-#1444 (desglose); #1290 (investigacion), #1411, #867, #890.
- `docs/referencia/modalidades_jornada_colombia.md`.
- CA-ADR-0034 (plantilla, enmendado), CA-ADR-0036 (ausencias, semana juzgable), CA-ADR-0045 (borrador; la estampa),
  CA-ADR-0029 / CA-ADR-0025 (eventos por rol y payload plano), CA-ADR-0039 (modalidad tecnica de las tools).
- Art. 161: <https://leyes.co/codigo_sustantivo_del_trabajo/161.htm>. Fuentes secundarias: El Tiempo, Notaria 19,
  Concepto Mintrabajo 54700/2023 (no trata el tope diario).
- Glosario (`docs/ddd/ubiquitous-language.yaml`): lo actualiza el planner al cierre de la sesion (Jornada,
  Modalidad de jornada, Advertencias de plantilla).

## Control de cambios

- 2026-11-07: creado (sesion planner 2026-11-01 y refinamiento 2026-11-07; registro en #1435). Decisiones 11 y 12
  segun el refinamiento.
- 2026-11-08: decision 10 precisada: `CalculadorAdvertencias` rige las advertencias sobre dias programados; la
  plantilla y la Jornada se auditan a si mismas en Programacion (#1438).
