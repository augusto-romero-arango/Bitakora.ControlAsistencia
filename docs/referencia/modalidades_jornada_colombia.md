# Modalidades legales de jornada en Colombia y sus reglas de control

**Documento de referencia para el ecosistema de reglas de asistencia de Colombia.** Reúne las modalidades de jornada que reconocen el Código Sustantivo del Trabajo (CST) y sus reformas, describe las reglas medibles de cada una y señala qué cubre hoy la Jornada del sistema y qué advertencias cambiarían o nacerían. Es hermano de `motor_horas_colombia_v2.md`: ese documento clasifica horas; este trata la **programación** y sus advertencias.

> ⚠️ **Fecha de corte: 2026-10-31. Este documento no es asesoría legal.** Un abogado laboralista colombiano debe validar toda regla con efecto en el cliente antes de llevarla a producción. El documento no decide nada sobre el modelo: las decisiones (CA-ADR y features) salen de una sesión planner con el experto (sección 8), igual que CA-ADR-0043 salió de la investigación #1333.

> **Metodología y límites de verificación.** Todas las fuentes son secundarias; la compilación oficial del Senado y la de Función Pública no respondieron en el refinamiento del 2026-10-31. Lo verificado y lo pendiente:
> - **Verificado** el 2026-10-31 contra la compilación de leyes.co (marcada «Vigente, con las modificaciones. Última actualización 26/10/2026»): el texto de los arts. 160 a 167 y 172 a 185 del CST. Las citas textuales de este documento salen de ahí.
> - **Verificado** contra accounter.co: el Concepto 342949 del 20-nov-2008 del Ministerio de la Protección Social, sobre los arts. 165 y 166.
> - **No releídos:** el texto directo de la Ley 2101/2021 y de la Ley 2466/2025, la Circular Mintrabajo 0101/2025, leyonline.co y la sentencia 2021-513 (PDF). Lo que sale de ellos se cita a través de `motor_horas_colombia_v2.md` o del issue #1290.
>
> Lo que no pudo contrastarse contra una fuente se marca **«no verificado»**. Las lecturas propias del texto (interpretaciones, no transcripciones) se marcan **«lectura del texto»**.

---

## 1. Vigencias que condicionan todo el documento

| Disposición | Efecto | Vigencia | Fuente |
|---|---|---|---|
| Ley 2101/2021 art. 3 (cuarta reducción) | La jornada máxima semanal baja a **42 h** | 15-jul-2026 (**vigente** a la fecha de corte) | Art. 161 CST (texto vigente: «cuarenta y dos (42) horas a la semana»); `motor_horas_colombia_v2.md` §1 |
| Ley 2466/2025 art. 11 | Nueva redacción del art. 161 CST | 25-jun-2025 | `motor_horas_colombia_v2.md` §1 |
| Ley 2466/2025 art. 10 | Trabajo diurno de 6:00 a.m. a 7:00 p.m. y nocturno de 7:00 p.m. a 6:00 a.m. (art. 160 CST) | 25-dic-2025 | Art. 160 CST (verificado); `motor_horas_colombia_v2.md` §1 |
| Ley 2466/2025 art. 12 | Suprime el permiso del Mintrabajo para horas extras y exige su registro (art. 162 num. 2 CST) | 25-jun-2025 | Art. 162 CST (verificado); `motor_horas_colombia_v2.md` §1 |
| Ley 2466/2025 art. 13 | Extras: máximo 2 h diarias y 12 h semanales (art. 167A CST) | 25-jun-2025 | `motor_horas_colombia_v2.md` §5 |
| Ley 2466/2025 art. 14 | Nuevo art. 179 CST: «dominical» = día de descanso obligatorio, pactable por escrito en otro día, y habitualidad por mes calendario | 25-jun-2025 | Art. 179 CST (verificado); `motor_horas_colombia_v2.md` §1 |
| Ley 2466/2025 art. 70 | Deroga el lit. b) del art. 162 CST: el servicio doméstico entra a la jornada máxima | 25-jun-2025 | `motor_horas_colombia_v2.md` §1 y §2 (el lit. b) aparece vacío en el texto verificado) |
| Circular Mintrabajo 0101/2025 | Lineamientos sobre jornada, extras y recargos tras la Ley 2466; tope combinado de 42 + 12 = 54 h semanales | 22-sep-2025 | `motor_horas_colombia_v2.md` §1 y §5 (**no verificado**: no se releyó aquí) |

Nota: `motor_horas_colombia_v2.md` está redactado a abril de 2026, con 44 h vigentes y 42 h «aún no vigente». A la fecha de corte de este documento ya rigen las 42 h.

---

## 2. Catálogo de modalidades (CA-1)

Convención: el campo «Combina» dice si la modalidad se superpone con otra en el mismo trabajador. Si la fuente no lo dice de forma expresa, se marca **no verificado**.

### M1. Jornada ordinaria máxima (art. 161 inc. 1 CST)
- **Aplica a:** todo trabajador que no esté excluido (art. 162) ni cubierto por una modalidad especial.
- **Contenido:** máximo 8 h al día y 42 h a la semana. La semana se distribuye «de común acuerdo […] de cinco (5) a seis (6) días a la semana, garantizando siempre el día de descanso». El inc. 2 agrega que «el número de horas de trabajo diario podrá distribuirse de manera variable», siempre dentro del tope diario.
- **Requisitos:** acuerdo sobre la distribución. El texto no exige forma escrita.
- **Aritmética a validar:** 42 h en 5 días son 8,4 h al día, por encima del tope de 8 h. Con el tope de 8 h, cinco días solo suman 40 h. El texto no dice cómo se concilia. Hay dos candidatos con nombre propio: la flexible (M2, hasta 9 h) y el art. 164 CST («descanso en la tarde del sábado»), que permite ampliar la jornada ordinaria «hasta por dos (2) horas, por acuerdo entre las partes, pero con el fin exclusivo de permitir a los trabajadores el descanso durante todo el sábado». El art. 164 todavía habla de «cuarenta y ocho (48) horas», la misma ambigüedad que tiene el art. 165. Ver §8.
- **Combina:** es la base; las demás la modifican o la sustituyen.
- Fuente: art. 161 y art. 164 CST (https://leyes.co/codigo_sustantivo_del_trabajo/161.htm).

### M2. Jornada flexible (art. 161 lit. a) CST)
- **Aplica a:** cualquier trabajador, si empleador y trabajador lo acuerdan.
- **Contenido:** la jornada semanal de 42 h se cumple «mediante jornadas diarias flexibles de trabajo, distribuidas en máximo seis días a la semana con un día de descanso obligatorio, que podrá coincidir con el día domingo». Las horas diarias varían «durante la respectiva semana, teniendo como mínimo cuatro (4) horas continuas y máximo hasta nueve (9) horas diarias». No hay recargo por trabajo suplementario «cuando el número de horas de trabajo no exceda el promedio de cuarenta y dos (42) horas semanales dentro de la Jornada Ordinaria. De conformidad con el artículo 160». El promedio se calcula **dentro de la semana**: la ley no le da un periodo de varias semanas.
- **Requisitos:** acuerdo («podrán acordar»). El texto no exige forma escrita.
- **Ambigüedad:** «dentro de la Jornada Ordinaria. De conformidad con el artículo 160» podría limitar la flexible a la franja diurna (6:00 a.m. a 7:00 p.m.): **no verificado**.
- **Combina:** es una forma de distribuir la ordinaria. Con el art. 165: **no verificado**.
- Fuente: art. 161 CST; `motor_horas_colombia_v2.md` §2.

### M3. Labores insalubres o peligrosas (art. 161 lit. b) CST)
- **Aplica a:** «labores que sean especialmente insalubres o peligrosas».
- **Contenido:** «el Gobierno puede ordenar la reducción de la jornada de trabajo de acuerdo con dictámenes al respecto». La ley no fija cifras. Si existe hoy algún decreto con cifras concretas: **no verificado**.
- **Requisitos:** una orden del Gobierno basada en dictámenes. No es un pacto entre las partes.
- **Combina:** **no verificado**.
- Fuente: art. 161 CST.

### M4. Menores de edad (art. 161 lit. c) CST)
- **Aplica a:** «menores de edad, autorizados para trabajar».
- **Contenido:**
  - De 15 a 17 años: «solo podrán trabajar en jornada diurna máxima de seis (6) horas diarias y treinta (30) horas a la semana».
  - Mayores de 17 años: «jornada máxima de ocho (8) horas diarias y cuarenta (40) horas a la semana». El texto del CST no les fija franja.
- **Franja:** «diurna» según el art. 160 CST es de 6:00 a.m. a 7:00 p.m. (verificado). `motor_horas_colombia_v2.md` §2 da cortes distintos: 6:00 p.m. para 15-17 años y 8:00 p.m. para 17-18 años. Probablemente vienen del Código de la Infancia y la Adolescencia: **no verificado**.
- **Requisitos:** autorización para trabajar (exigida por el texto). Quién la otorga y cómo: **no verificado**.
- **Combina:** los topes de menores son más restrictivos que los de cualquier otra modalidad. Que excluyan las extensiones de M2, M6 y M7 es una inferencia: **no verificado**.
- Fuente: arts. 160 y 161 CST; `motor_horas_colombia_v2.md` §2.

### M5. Turnos sucesivos sin solución de continuidad (art. 161 lit. d) CST)
- **Aplica a:** empresas, o secciones de ellas, que operan «sin solución de continuidad durante todos los días de la semana» con turnos sucesivos.
- **Contenido:**
  - El turno «no exceda de seis (6) horas al día y treinta y seis (36) a la semana».
  - No hay recargo nocturno ni dominical (ver `motor_horas_colombia_v2.md` §2).
  - El trabajador «tendrá derecho a un día de descanso remunerado».
- **Descanso propio:** esta es la «jornada de treinta y seis (36) horas semanales del artículo 20 literal c)» de la Ley 50/1990, la única excepción que hace el art. 172 al descanso dominical. Si labora el día de descanso, solo tiene derecho a descanso compensatorio remunerado (arts. 175 lit. d, 180 y 181). Ver §4.
- **Requisitos:** acuerdo entre empleador y trabajador, «temporal o indefinidamente», y una operación continua todos los días de la semana. `motor_horas_colombia_v2.md` la trata como una marca contractual.
- **Combina:** **no verificado**. M5 y M7 cubren las dos labores continuas; son regímenes alternativos, uno con 36 h sin recargos y otro con hasta 56 h (lectura del texto).
- Fuente: arts. 161, 172, 175, 180 y 181 CST.

### M6. Trabajo por turnos con promedio (art. 165 CST)
- **Aplica a:** labores donde «la naturaleza de la labor no exija actividad continuada y se lleve a cabo por turnos de trabajadores» (texto del artículo; lo reitera el Concepto 342949/2008).
- **Contenido:** la jornada puede pasar de 8 h diarias o de 48 h semanales «siempre que el promedio de las horas de trabajo calculado para un período que no exceda de tres (3) semanas, no pase de ocho (8) horas diarias ni de cuarenta y ocho (48) a la semana». No es trabajo suplementario. **El promedio aplica tanto al tope diario como al semanal.**
- **Requisitos:** la naturaleza de la labor (no continua) y la organización por turnos. El issue también exige un «acuerdo»: ni el texto del art. 165 ni el Concepto 342949/2008 lo mencionan, así que queda **no verificado**.
- **Combina:** M6 excluye a M5 y M7: el art. 165 pide una labor que «no exija actividad continuada», y los arts. 161 lit. d) y 166 piden una labor «sin solución de continuidad» (lectura del texto; confirmar con el abogado).
- **Ambigüedad conocida:** el «48» del artículo se lee en concordancia con el art. 161 (42 h hoy). Ver §8.
- Fuentes:
  - https://leyes.co/codigo_sustantivo_del_trabajo/165.htm
  - https://leyonline.co/laws/codigo-sustantivo-del-trabajo/articulo-165-trabajo-por-turnos
  - Concepto 342949/2008: https://accounter.co/normatividad/conceptos/precisiones-sobre-distribucion-por-turnos-de-la-jornada-laboral
  - Tribunal Superior de Bogotá, rad. 2021-513: turnos 7x4 y 7x3 bajo el art. 165, con un promedio de 144 h en 3 semanas (según el issue #1290; no se verificó el detalle horario de esos turnos).

### M7. Trabajo sin solución de continuidad (art. 166 CST)
- **Aplica a:** «labores que por razón de su misma naturaleza necesiten ser atendidas sin solución de continuidad, por turnos sucesivos de trabajadores».
- **Contenido:** se puede elevar el límite del art. 161, pero «las horas de trabajo no pueden exceder de cincuenta y seis (56) por semana». El texto no fija tope diario.
- **Requisitos:** según el Concepto 342949/2008, la empresa debe comprobar la necesidad técnica de operar sin interrupción ante la dirección territorial o el inspector del trabajo, con base en el art. 4 del Decreto 995 de 1968. La vigencia actual de ese decreto: **no verificado**. El texto no exige pacto con el trabajador.
- **Combina:** M7 excluye a M6 (ver M6). Con M5: **no verificado**. `motor_horas_colombia_v2.md` §2 lo trata como «tope absoluto de 56 h sin generar extras».
- Fuente: https://leyes.co/codigo_sustantivo_del_trabajo/166.htm; Concepto 342949/2008.

### M8. Excluidos de la jornada máxima (art. 162 num. 1 CST)
- **Aplica a:**
  - lit. a): cargos de dirección, de confianza o de manejo.
  - lit. b): aparece vacío. Lo derogó el art. 70 de la Ley 2466/2025, según `motor_horas_colombia_v2.md`; antes, la sentencia C-372/1998 ya había declarado inexequible la exclusión.
  - lit. c): labores discontinuas o intermitentes y de simple vigilancia, «cuando residan en el lugar o sitio de trabajo».
  - lit. d): «choferes mecánicos que presten sus servicios en empresas de transporte de cualquier clase». Aparece en el texto compilado, pero su vigencia se discute: **no verificado**.
- **Contenido:** «Quedan excluidos de la regulación sobre la jornada máxima legal de trabajo». No hay tope de horas. El texto no menciona el descanso (ver §4).
- **Requisitos:** la condición del cargo o de la labor, no un pacto de jornada. En vigilancia y labores discontinuas, además, que el trabajador resida en el sitio.
- **Combina:** mientras dure la exclusión no aplica ninguna de M1 a M7, porque el tope no rige.
- Fuente: https://leyes.co/codigo_sustantivo_del_trabajo/162.htm; `motor_horas_colombia_v2.md` §2.

**Otras figuras fuera del catálogo mínimo, anotadas para no perderlas:**
- **Art. 163 CST, fuerza mayor y urgencia.** El límite se puede elevar «por razón de fuerza mayor, caso fortuito, de amenazar u ocurrir algún accidente» o por trabajos de urgencia, «únicamente […] en la medida necesaria». El empleador debe registrar esas horas. Es excepcional y no se programa.
- **Art. 164 CST, ampliación para descansar el sábado.** Ver M1.
- **Art. 182 CST, «Técnicos».** Quienes no pueden reemplazarse sin grave perjuicio para la empresa trabajan domingos y festivos sin descanso compensatorio. Ver §4.
- **Art. 184 CST, labores que no pueden suspenderse.** El descanso se acumula. Ver §4.
- **Sectores con régimen propio** (seguridad, Ley 1920/2018; salud): el detalle de su jornada está **no verificado**. Están exceptuados del tope de extras del art. 167A (`motor_horas_colombia_v2.md` §5).

---

## 3. Fichas de reglas medibles (CA-2)

Cada ficha recoge siete reglas: **tope diario / mínimo diario / horas semanales / periodo de medición / descanso y ventana / franja horaria / dos turnos el mismo día**. «n.a.» significa que la norma no lo regula, que no es lo mismo que «sin límite».

### F1. Ordinaria máxima (161 inc. 1)
- **Tope diario:** 8 h.
- **Mínimo diario:** n.a.
- **Horas semanales:** 42 h como tope.
- **Periodo:** la semana.
- **Descanso:** el día de descanso garantizado (161), de al menos 24 h (172), que es el domingo salvo que se pacte otro día por escrito (179 par. 3). La distribución en 5 o 6 días de trabajo implica 1 o 2 días de descanso por semana (lectura del texto).
- **Franja:** n.a. para adultos.
- **Dos turnos el mismo día:** prohibido (161 par. 1).
- El texto habla de «semana» sin definir si va de lunes a domingo: **no verificado**.

### F2. Flexible (161 lit. a)
- **Tope diario:** 9 h.
- **Mínimo diario:** 4 h **continuas**.
- **Horas semanales:** 42 h; las horas diarias se promedian dentro de la semana.
- **Periodo:** «la respectiva semana».
- **Descanso:** un día obligatorio, que puede coincidir con el domingo; máximo 6 días de trabajo.
- **Franja:** «dentro de la Jornada Ordinaria […] artículo 160»; si eso la limita a la franja diurna: **no verificado**.
- **Dos turnos el mismo día:** prohibido.

### F3. Insalubres o peligrosas (161 lit. b)
- **Topes:** los ordena el Gobierno con base en dictámenes; las cifras están **no verificadas**.
- **Descanso:** el general (172).
- **Dos turnos el mismo día:** prohibido (161 par. 1).

### F4. Menores (161 lit. c)
- **Tope diario:** 6 h (15-17 años) / 8 h (mayores de 17).
- **Mínimo diario:** n.a.
- **Horas semanales:** 30 h / 40 h como tope.
- **Periodo:** la semana.
- **Descanso:** el general (172).
- **Franja:**
  - 15-17 años: diurna, de 6:00 a.m. a 7:00 p.m. según el art. 160. El corte de 6:00 p.m. que da `motor_horas_colombia_v2.md` está **no verificado**.
  - Mayores de 17: el CST no fija franja; el corte de 8:00 p.m. de `motor_horas_colombia_v2.md` está **no verificado**.
- **Dos turnos el mismo día:** prohibido.

### F5. Turnos sucesivos (161 lit. d)
- **Tope diario:** 6 h.
- **Mínimo diario:** n.a.
- **Horas semanales:** 36 h como tope.
- **Periodo:** la semana.
- **Descanso:** un día remunerado; está exceptuado del descanso **dominical** (172) y, si trabaja el día de descanso, solo recibe compensatorio (175 lit. d, 180, 181).
- **Franja:** n.a. (la nocturna y la dominical no generan recargo; ver `motor_horas_colombia_v2.md`).
- **Dos turnos el mismo día:** prohibido.

### F6. Turnos con promedio (165)
- **Tope diario:** un día puede pasar de 8 h si el **promedio diario** del periodo no pasa de 8 h. Un techo diario absoluto distinto del límite de 24 h del turno: **no verificado**.
- **Mínimo diario:** n.a.
- **Horas semanales:** el promedio no puede superar el tope (48 h según el texto, 42 h según el art. 161: ver §8).
- **Periodo:** **hasta 3 semanas**. La ley no dice si el periodo es fijo o móvil ni quién lo define: **no verificado**.
- **Descanso:** no se exime (172).
- **Franja:** n.a.
- **Dos turnos el mismo día:** prohibido.

### F7. Sin solución de continuidad (166)
- **Tope diario:** el texto no lo fija.
- **Mínimo diario:** n.a.
- **Horas semanales:** hasta 56 h.
- **Periodo:** la semana («por semana»).
- **Descanso:** el general (172). Si el trabajador no puede tomarlo en el curso de la semana, ver art. 184 en §4 (aplicación a 166: **no verificado**).
- **Franja:** n.a.
- **Dos turnos el mismo día:** prohibido; el par. 1 del 161 no distingue modalidad (lectura del texto).

### F8. Excluidos (162)
- **Tope diario y semanal:** no rige.
- **Mínimo diario:** n.a.
- **Periodo:** n.a.
- **Descanso:** no se exime (ver §4).
- **Franja:** n.a.
- **Dos turnos el mismo día:** el par. 1 del 161 exceptúa la supervisión, la dirección, la confianza y el manejo. La vigilancia residente (lit. c) y los choferes (lit. d) no están en esa excepción (lectura del texto).

### Tabla resumen modalidad x regla

| Modalidad | Tope diario | Mín. diario | Horas semanales | Periodo medición | Descanso | Franja | Dos turnos/día |
|---|---|---|---|---|---|---|---|
| M1 Ordinaria (161) | 8 h | n.a. | 42 h (tope) | Semana | 1 o 2 días (5-6 de trabajo); 24 h; domingo o pactado | n.a. | Prohibido |
| M2 Flexible (161 a) | 9 h | 4 h continuas | 42 h (promedio en la semana) | Semana | 1 día; ≤ 6 días de trabajo | ¿Jornada ordinaria? (no verificado) | Prohibido |
| M3 Insalubres (161 b) | Los ordena el Gobierno | n.a. | Los ordena el Gobierno | no verificado | General | n.a. | Prohibido |
| M4 Menores (161 c) | 6 h / 8 h | n.a. | 30 h / 40 h | Semana | General | 15-17: diurna (6:00-19:00, art. 160) | Prohibido |
| M5 Sucesivos (161 d) | 6 h | n.a. | 36 h | Semana | 1 día remunerado; sin dominical; solo compensatorio | n.a. | Prohibido |
| M6 Turnos (165) | Promedio ≤ 8 h | n.a. | Promedio ≤ 48 h / 42 h | Hasta 3 semanas | General | n.a. | Prohibido |
| M7 Continuidad (166) | Sin tope en el texto | n.a. | ≤ 56 h | Semana | General | n.a. | Prohibido |
| M8 Excluidos (162) | No rige | n.a. | No rige | n.a. | No se exime | n.a. | Excepción dirección/confianza/manejo/supervisión |

---

## 4. Descanso (CA-3)

**¿Existe una modalidad que exima del descanso?** Ninguna modalidad de **jornada** (M1 a M8) suprime el descanso semanal. Sí hay figuras del CST que lo cambian, y una que suprime el descanso compensatorio:

1. **Art. 182 CST, «Técnicos».** Es lo más cercano a «el empleado no necesita descansos». El texto dice: «Las personas que por sus conocimientos técnicos o por razón del trabajo que ejecutan no puede reemplazarse sin grave perjuicio para la empresa, deben trabajar los domingos y días de fiesta sin derecho al descanso compensatorio, pero su trabajo se remunera conforme al artículo 179». Su vigencia y alcance frente a la Ley 2466/2025 y a la jurisprudencia están **no verificados**: el artículo aparece en la compilación vigente, pero ningún concepto ni sentencia que lo aplique se releyó aquí.
2. **M5, turnos sucesivos (161 lit. d).** Es la única excepción expresa al descanso **dominical**: el art. 172 obliga «a todos sus trabajadores», «salvo la excepción consagrada en el literal c) del artículo 20 de esta ley». Los arts. 175 lit. d), 180 y 181 identifican esa excepción como la «jornada de treinta y seis (36) horas semanales». La modalidad conserva un día de descanso remunerado que no tiene que caer en domingo, y el trabajo en el día de descanso solo da derecho a compensatorio, no a dinero.
3. **Art. 184 CST, labores que no pueden suspenderse** («como los viajes fluviales o marítimos»). Si el personal no puede tomar el descanso «en el curso de una o más semanas, se acumulan los días de descanso en la semana siguiente a la terminación de las labores o se paga la correspondiente remuneración en dinero, a opción del trabajador». El descanso se aplaza, no se suprime. Es relevante para los ciclos largos (14x14, 20x10). Si aplica fuera de esos ejemplos: **no verificado**.
4. **M8, excluidos (162).** Los excluye «de la regulación sobre la jornada máxima legal», y el texto no menciona el descanso. El art. 172 obliga a darlo a «todos sus trabajadores» con una sola excepción (M5). Por la letra, la exclusión no alcanza al descanso (lectura del texto; la confirmación jurisprudencial está **no verificada**).

**Pregunta abierta para el experto:** ¿qué tenía en mente al decir «el empleado no necesita descansos»? ¿El técnico irreemplazable del art. 182, la jornada de 36 h sin dominical (M5) o los descansos acumulados del art. 184?

Reglas transversales a todas las modalidades (texto verificado salvo donde se indica):

1. **Descanso dominical remunerado de al menos 24 h** (art. 172) para todos los trabajadores, salvo la jornada de 36 h (M5).
2. **Día de descanso obligatorio = domingo, o el día que se pacte por escrito** (art. 179 par. 2 y 3). Para el Código, «dominical» significa «día de descanso obligatorio». Si el contrato no dice otra cosa, se presume el domingo.
3. **Dónde se permite trabajar en el día de descanso** (art. 175). El texto dice que «solamente se permite» con retribución o con descanso compensatorio remunerado, en cuatro casos:
   - labores que no sean susceptibles de interrupción;
   - necesidades inaplazables (servicios públicos, drogas y alimentos);
   - servicio doméstico y choferes particulares;
   - la jornada de 36 h, con solo compensatorio.
   El Gobierno especifica las labores de los dos primeros casos. Cómo se lee hoy esa restricción frente al trabajo dominical habitual en otras actividades: **no verificado**.
4. **Habitualidad y compensatorio** (arts. 179 par. 1, 180, 181, 183 y 185):
   - El trabajo en el día de descanso es **ocasional** si son hasta 2 días en el **mes calendario**; con el ocasional, el trabajador elige entre compensatorio remunerado o retribución en dinero (180).
   - Es **habitual** si son 3 o más; con el habitual, recibe compensatorio remunerado sin perjuicio de la retribución en dinero (181).
   - El compensatorio se da «en otro día laborable de la semana siguiente» o «desde el medio día o a las trece horas (1 p.m.) del domingo, hasta el medio día o a las trece horas (1 p.m.) del lunes» (183).
   - Para los trabajos habituales en domingo, el empleador publica con al menos 12 h de anticipación la relación del personal sin descanso dominical, con el día y las horas del compensatorio (185).
   - Detalle de la habitualidad (SL3567-2019, sentencia 10079/1997): ver `motor_horas_colombia_v2.md` §7.
5. **Dos turnos el mismo día** (art. 161 par. 1): «El empleador no podrá, aún con el consentimiento del trabajador o trabajadora, contratarla para la ejecución de dos turnos en el mismo día, salvo en labores de supervisión, dirección, confianza o manejo».
6. **Pausa dentro de la jornada** (art. 167): «Las horas de trabajo durante cada jornada deben distribuirse al menos en dos secciones, con un intermedio de descanso […]. El tiempo de este descanso no se computa en la jornada».
7. **Tope de extras programadas** (art. 167A, vía `motor_horas_colombia_v2.md` §5): máximo 2 h diarias y 12 h semanales, salvo los sectores de seguridad y salud. Esta regla es de programación solo si el turno lleva franja de extras.

Alcance: este documento trata la **programación** del descanso, no su remuneración ni sus recargos (ver `motor_horas_colombia_v2.md`).

---

## 5. Efecto en la Jornada y en las advertencias (CA-4)

### 5.1 Lo que existe hoy

- **Jornada** (glosario «Jornada», `LimitesJornada`). Tiene cuatro parámetros obligatorios:
  - horas semanales, como **valor exacto** (más = exceso, menos = subprogramación);
  - tope diario;
  - mínimo diario (0 si no aplica);
  - días de descanso por semana, como valor exacto de 0 a 6 (0 = no se controla).
  Según el glosario, «el sistema no impone modalidades legales ni certifica cumplimiento».
- **Advertencias de plantilla** (`AuditoriaPlantillaTurnos`, `TipoAdvertenciaPlantilla`). Hoy la plantilla solo emite `PlantillaSinJornada` y las diarias: `DiaSinTurno`, `SuperaTopeDiario` y `PorDebajoDelMinimoDiario`. Las cuatro semanales (`SuperaHorasSemanales`, `PorDebajoDeHorasSemanales`, `FaltanDiasDeDescanso` y `SobranDiasDeDescanso`) siguen en el enum pero sin uso desde #1292. La entrada «Advertencias de plantilla» del glosario todavía las describe como aplicables; actualizarla le corresponde al planner.
- **Advertencias de la programación semanal** (`CalculadorAdvertencias`, semana de lunes a domingo).
  - **Semanales:** las mismas cuatro. Una semana es **juzgable** si tiene sus 7 días programados (trabajo o descanso) y **ninguna ausencia**; la ausencia aporta 0 horas (glosario; CA-ADR-0036). En una semana juzgable, las horas semanales y los días de descanso (si el parámetro es mayor que 0) se miden exactos. En una semana no juzgable solo se juzga el exceso de horas.
  - **Diarias:** `SuperaTopeDiario` y `PorDebajoDelMinimoDiario` (solo en días de trabajo, siempre), `PorDebajoDelDescansoEntreTurnos` y `SeSolapaConTurnoDeLaVispera`.
  - El descanso entre turnos usa un mínimo fijo de 6 h, que es la suma de los márgenes de la cobertura de marcación (CA-ADR-0042). **No es** una regla legal de jornada.

### 5.2 Cobertura por modalidad

Leyenda: ✅ cubre bien, ◐ cubre con límites, ✖ no cubre.

| Modalidad | (a) Qué cubre hoy la Jornada | Qué falta | (b) Advertencias que cambian o nacen |
|---|---|---|---|
| M1 Ordinaria | Horas semanales 42, tope diario 8, descansos 1 (✅) | ◐ La distribución en 5 o 6 días solo se controla con `DiasDescansoPorSemana`, que es un valor exacto: no admite «1 o 2». ✖ No hay regla de 24 h continuas ni de día de descanso obligatorio (domingo o pactado, 179 par. 3). ✖ Compensatorio y habitualidad (179 par. 1, 183). | Plantilla: las semanales solo tienen sentido si el ciclo es múltiplo de 7 (ver 5.3). Programación: podría nacer «falta compensatorio» (semana siguiente al día de descanso trabajado), con la habitualidad medida por **mes calendario**. |
| M2 Flexible | Tope diario 9, mínimo 4, horas semanales 42, descansos 1 (✅ con valores propios; el promedio es dentro de la semana, así que la semana de lunes a domingo sirve) | ◐ «4 horas **continuas»**: el mínimo diario mide minutos ordinarios, no continuidad. Franja «dentro de la jornada ordinaria» (no verificado). | Ningún periodo nuevo. Podría nacer una diaria de «mínimo continuo» si el turno parte las 4 h. |
| M3 Insalubres | Se aproxima con valores propios (✅ si se conocen las cifras) | La orden del Gobierno (no verificada) no se registra. | Ninguna nueva; depende de las cifras. |
| M4 Menores | Horas y tope diario (✅) | ✖ **Franja horaria permitida**: no es un parámetro de la Jornada. ✖ Autorización para trabajar. | Nace una advertencia de franja («fuera de la franja permitida»), en plantilla y en programación. |
| M5 Sucesivos | 6 h y 36 h, descansos 1 (✅) | El descanso no tiene por qué ser el domingo; la Jornada ya no lo ata a un día (✅). Recargos: son del motor. | Ninguna nueva en programación. Si nace «falta compensatorio» (M1), M5 cambia la regla: solo compensatorio. |
| M6 Turnos 165 | ✖ Ni el tope diario ni las horas semanales expresan «promedio en hasta 3 semanas». | ✖ **Periodo de medición** configurable. ✖ Tope diario **como promedio**, no por día. | Plantilla: nace una advertencia de promedio por periodo o por ciclo, y `SuperaTopeDiario` pasaría a juzgar el promedio. Programación: la medición de 3 semanas rebasa la semana de lunes a domingo. Con ausencias, una sola ausencia hoy vuelve no juzgable la semana; en un periodo de 3 semanas habría que decidir si invalida el periodo entero o si el día ausente cuenta con horas (CA-ADR-0036). |
| M7 Continuidad | ◐ Horas 56 como valor exacto, cuando la ley da un techo | ✖ No hay tope diario en el texto, pero el parámetro es obligatorio en la Jornada. ✖ Descansos acumulados (184, no verificado para 166). ✖ Comprobación ante el inspector. | `SuperaHorasSemanales` funciona como tope; `PorDebajoDeHorasSemanales` pierde sentido (es un techo, no un valor exacto); `SuperaTopeDiario` no aplicaría. |
| M8 Excluidos | ◐ Se aproxima con valores holgados: el sistema no puede decir «sin tope». | ✖ No hay forma de representar «no aplica el tope». | `SuperaTopeDiario` y `SuperaHorasSemanales` dejarían de aplicar; el descanso se seguiría verificando. |

Notas transversales:
- **Valor exacto contra techo.** En casi todas las modalidades la ley da un **techo**, y la Jornada vigente trata las horas semanales como valor **exacto**. Es una diferencia de fondo (ver §8).
- **Dos turnos el mismo día (161 par. 1).** No hay advertencia hoy. `SeSolapaConTurnoDeLaVispera` y `PorDebajoDelDescansoEntreTurnos` miden otra cosa: la cobertura de marcación.

### 5.3 Caso trabajado: 6x2 de 8 h contra 42 h

El ciclo 6x2 son 6 días de 8 h seguidos de 2 de descanso. El ciclo dura 8 días y suma 48 h de trabajo: 6 h por día de ciclo, es decir 42 h por semana en promedio. Ajusta exactamente a 42 h en promedio, pero solo sobre el ciclo completo.

- **Semana calendario de lunes a domingo (programación).** El ciclo es de 8 días y la semana de 7, así que cada semana contiene 5 o 6 días de trabajo: 40 h o 48 h. Las semanas **no se alternan**: de cada 8 semanas consecutivas, 2 seguidas son de 48 h y 6 son de 40 h (6 × 40 + 2 × 48 = 336 h = 8 × 42). Contra una Jornada de 42 h exactas, `CalculadorAdvertencias` emite:
  - en cada semana de 48 h, `SuperaHorasSemanales` de 360 min;
  - en cada semana de 40 h juzgable, `PorDebajoDeHorasSemanales` de 120 min.

  Con días de descanso, las semanas de 48 h tienen 1 día de descanso y las de 40 h tienen 2:
  - con `DiasDescansoPorSemana = 1`, las 6 semanas de 40 h suman además `SobranDiasDeDescanso` (1);
  - con `DiasDescansoPorSemana = 2`, las 2 semanas de 48 h suman `FaltanDiasDeDescanso` (1).

  En los dos casos, **todas las semanas** tienen al menos una advertencia, aunque el diseño cumpla el promedio de 42 h.
- **Plantilla (ciclo de N días).** Con N = 8 las advertencias semanales no tienen definición, porque no hay «semana»; por eso, desde #1292, la plantilla solo emite diarias. Un 6x2 de 8 h pasa limpio las diarias (tope 8 y mínimo ≤ 8) si los 2 días de descanso llevan un turno de descanso; sin él, cada uno da `DiaSinTurno`.
- **Encaje legal.** El 6x2 no tiene nombre en la ley. Su encaje más probable es el art. 165 (turnos con promedio de hasta 3 semanas) si la labor no exige actividad continua, o el art. 166 / 161 lit. d) si la labor es continua; en el 161 lit. d) no cabe un turno de 8 h. Medido en periodos de 3 semanas (21 días), los días trabajados varían entre **15, 16 y 17**, es decir 120, 128 o 136 h. El promedio semanal queda en 40, 42,67 o 45,33 h. Con bloques fijos de 3 semanas alineados al día 1 del ciclo, 4 de cada 8 bloques superan las 42 h y ninguno supera las 48 h. **El 6x2 de 8 h cumple el art. 165 si el tope es 48 h y lo incumple en la mitad de los periodos si el tope es 42 h** (aritmética verificable; la lectura del tope es el punto 1 de §8). La medición que lo deja exacto es el ciclo completo o un múltiplo común: 56 días = 8 semanas = 7 ciclos = 336 h = 42 h/semana. Si la ley admite un periodo mayor de 3 semanas para este caso: **no verificado**.
- **Lo que haría falta:** un periodo de medición distinto de la semana (el ciclo, o hasta 3 semanas, fijo o móvil) y una advertencia de promedio contra ese periodo. Para esa modalidad, la semana de lunes a domingo dejaría de ser la unidad de juicio.

---

## 6. Resumen de huecos de la Jornada

1. **Periodo de medición:** no existe; la única unidad es la semana de lunes a domingo. Lo necesitan M6 (hasta 3 semanas), todo ciclo que no sea múltiplo de 7 y la habitualidad dominical (mes calendario, 179 par. 1).
2. **Techo contra valor exacto:** las horas semanales son exactas, y la ley da topes o promedios (M1, M2, M4, M5, M6, M7). Lo mismo pasa con los días de descanso: M1 admite 1 o 2.
3. **Franja horaria permitida:** no existe (M4; posiblemente M2).
4. **Tope diario como promedio** (M6) y **sin tope diario** (M7, M8): el tope diario es obligatorio y se juzga día por día.
5. **Descanso:**
   - solo existe el conteo de días por semana;
   - no hay día de descanso obligatorio (domingo o pactado, 179 par. 3) ni regla de 24 h continuas (172);
   - no hay compensatorio en la semana siguiente (183) ni descanso acumulado (184).
6. **Forma de decir «no aplica» (M8):** el 0 solo existe para los descansos y el mínimo diario.
7. **Marca de modalidad, pacto o autorización:** no hay dónde registrar:
   - el acuerdo (M2, M5);
   - la orden del Gobierno (M3);
   - la autorización del menor (M4);
   - la naturaleza de la labor (M6, M7) ni la comprobación ante el inspector (M7).
8. **Continuidad y pausa:** el mínimo de 4 h continuas (M2) y el intermedio de descanso dentro de la jornada (167) no se miden.
9. **Dos turnos el mismo día (161 par. 1):** no hay advertencia.

---

## 7. Fuentes

- CST, arts. 160 a 167 y 172 a 185, texto verificado el 2026-10-31 en https://leyes.co/codigo_sustantivo_del_trabajo/ (`<art>.htm`).
- Art. 165 en https://leyonline.co/laws/codigo-sustantivo-del-trabajo/articulo-165-trabajo-por-turnos (fuente del issue; no releída).
- Ley 2101/2021 art. 3; Ley 2466/2025 arts. 10 a 14 y 70; Circular Mintrabajo 0101/2025; art. 167A CST. Citados a través de `motor_horas_colombia_v2.md`; el texto directo no se releyó.
- Concepto 342949 del 20-nov-2008, Ministerio de la Protección Social (arts. 165 y 166; Decreto 995/1968 art. 4): https://accounter.co/normatividad/conceptos/precisiones-sobre-distribucion-por-turnos-de-la-jornada-laboral
- Tribunal Superior de Bogotá, rad. 2021-513: https://portalhistorico.ramajudicial.gov.co/documents/35753502/83891604/SENTENCIA+2021-513-01+TRABAJO+POR+TURNO+%281%29.pdf/462e1d0f-1554-4c51-9ffd-95a3aa36e135 (no releída).
- Corte Constitucional C-372/1998; CSJ SL3567-2019 y sentencia 10079/1997. Citadas a través de `motor_horas_colombia_v2.md`.
- Fuentes internas:
  - `docs/ddd/ubiquitous-language.yaml`: entradas «Jornada», «Advertencias de plantilla», «Advertencias de la programacion semanal» y «Descanso entre turnos».
  - Código: `AuditoriaPlantillaTurnos.cs`, `TipoAdvertenciaPlantilla.cs` y `Advertencias.cs` (`CalculadorAdvertencias`).
  - ADRs: CA-ADR-0034, CA-ADR-0036 y CA-ADR-0042.

---

## 8. Puntos para decidir con el experto (CA-6)

Esta sección **no decide nada**.

1. **Tope del art. 165.** El texto dice 48 h semanales y el art. 161 vigente dice 42 h. ¿Contra cuál se juzga el promedio? El 6x2 de 8 h cumple con 48 h y falla en la mitad de los periodos de 3 semanas con 42 h (§5.3). La misma pregunta vale para el «48» del art. 164.
2. **42 h en 5 días.** Con el tope diario de 8 h, cinco días suman 40 h. ¿Se exige la flexible (M2), el art. 164 o se admiten 8,4 h diarias en la ordinaria?
3. **Nombre del concepto.** «Modalidad de jornada» es provisional: «modalidad» ya nombró a la plantilla semanal histórica, y `motor_horas_colombia_v2.md` dice «régimen». ¿Cuál es el término del lenguaje ubicuo?
4. **Tensión con el principio de la Jornada.** Hoy la Jornada «no impone modalidades legales ni certifica cumplimiento». Si el sistema registra la modalidad y arma advertencias, ¿ese principio se enmienda o se reemplaza? ¿El sistema certifica o solo advierte?
5. **Convivencia.** ¿La modalidad legal convive con una Jornada pactada libre como la de hoy (Jornada sin modalidad), o toda Jornada nace con modalidad?
6. **Dónde se asigna.** ¿En la Jornada (catálogo) o en la Vinculación del colaborador? Hoy la Jornada se asigna a la Vinculación y se estampa al programar. Algunas condiciones son de la persona (menor, técnico irreemplazable del art. 182, cargo de dirección) y otras de la labor o de la empresa (M5, M6, M7).
7. **«Sin descansos».** ¿Qué figura tenía en mente el experto: el art. 182, M5 sin dominical o el art. 184? (§4)
8. **Techo contra valor exacto** de las horas semanales y de los días de descanso (§5.2, §6).
9. **Ausencias en periodos largos.** Con la regla actual, una ausencia vuelve no juzgable su semana. En un periodo de 3 semanas o de un ciclo, ¿invalida el periodo entero o el día ausente cuenta con horas (CA-ADR-0036)?
10. **Ciclos que no encajan** (6x2). ¿Se miden por ciclo, por periodo de hasta 3 semanas o se exige un múltiplo de 7? Si es por periodo, ¿fijo (desde qué fecha) o móvil?
11. **Día de descanso obligatorio y compensatorio.** ¿El sistema debe conocer el día de descanso pactado (179 par. 3) y advertir sobre el compensatorio de la semana siguiente (183) o sobre la habitualidad del mes calendario (179 par. 1)?
12. **Verificaciones pendientes** marcadas «no verificado»:
    - las cifras de M3;
    - los cortes horarios de M4;
    - las combinaciones entre modalidades;
    - el «acuerdo» del art. 165;
    - la vigencia del art. 162 lit. d), del art. 182 y del Decreto 995/1968;
    - el alcance del art. 175;
    - el alcance de la exclusión del art. 162 sobre el descanso.
