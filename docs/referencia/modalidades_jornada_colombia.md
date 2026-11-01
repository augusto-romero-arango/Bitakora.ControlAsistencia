# Modalidades legales de jornada en Colombia y sus reglas de control

**Documento de referencia para el ecosistema de reglas de asistencia de Colombia.** Cataloga las modalidades de jornada que el Código Sustantivo del Trabajo (CST) y sus reformas reconocen, ficha las reglas medibles de cada una y señala qué cubre hoy la Jornada del sistema y qué advertencias cambiarían o nacerían. Es hermano de `motor_horas_colombia_v2.md` (que clasifica horas; este trata la **programación** y sus advertencias).

> ⚠️ **Fecha de corte: 2026-10-31. Este documento no es asesoría legal.** Toda regla con efecto en el cliente debe validarla un abogado laboralista colombiano antes de producción. No decide nada de modelo: las decisiones (CA-ADR y features) salen de una sesión planner con el experto (sección 8), como CA-ADR-0043 salió de la investigación #1333.

> **Metodología y límites de verificación.** Las fuentes son secundarias (leyes.co, leyonline.co, accounter.co, portal histórico de la Rama Judicial); la compilación oficial del Senado y Función Pública no respondieron en el refinamiento del 2026-10-31. Este documento se redactó sin acceso web adicional: lo que no pudo contrastarse contra una de esas fuentes se marca **«no verificado»**. El texto de los artículos se parafrasea, no se transcribe.

---

## 1. Vigencias que condicionan todo el documento

| Disposición | Efecto | Vigencia | Fuente |
|---|---|---|---|
| Ley 2101/2021 art. 3 (cuarta reducción) | Jornada máxima semanal baja a **42 h** | 15-jul-2026 (**vigente** a la fecha de corte) | Ley 2101/2021; Ley 2466/2025 art. 11 |
| Ley 2466/2025 art. 11 | Nueva redacción del art. 161 CST | 25-jun-2025 | Ley 2466/2025 |
| Ley 2466/2025 art. 10 | Franja nocturna desde las 7:00 p.m. | 25-dic-2025 | `motor_horas_colombia_v2.md` §1 |
| Ley 2466/2025 art. 70 | Deroga el lit. b) del art. 162 CST (servicio doméstico entra a la jornada máxima) | 25-jun-2025 | `motor_horas_colombia_v2.md` §1 y §2 |
| Circular Mintrabajo 0101/2025 | Lineamientos de jornada y recargos tras la Ley 2466 | 22-sep-2025 | `motor_horas_colombia_v2.md` §1 (no releída aquí: **no verificado** el detalle sobre jornada) |

Nota: `motor_horas_colombia_v2.md` está redactado a abril de 2026 (44 h vigentes, 42 h «aún no vigente»). A la fecha de corte de este documento, 42 h ya rige.

---

## 2. Catálogo de modalidades (CA-1)

Convención: «Combina» dice si la modalidad se superpone con otra en el mismo trabajador. En ausencia de texto expreso en la fuente, se marca **no verificado**.

### M1. Jornada ordinaria máxima — art. 161 inc. 1 CST
- **Aplica a:** todo trabajador no excluido (art. 162) ni cubierto por una modalidad especial.
- **Contenido:** 8 h diarias y 42 h semanales, distribuibles en 5 o 6 días con día de descanso.
- **Requisitos:** distribución de común acuerdo entre las partes (en 5 o 6 días). Pacto escrito: no se exige para la ordinaria (**no verificado** el detalle de la distribución).
- **Combina:** es la base; las demás la modifican o la sustituyen.
- Fuente: https://leyes.co/codigo_sustantivo_del_trabajo/161.htm

### M2. Jornada flexible — art. 161 lit. a) CST
- **Aplica a:** cualquier trabajador cuyas partes lo pacten.
- **Contenido:** las 42 h semanales se distribuyen en **4 a 9 h diarias**, máximo 6 días a la semana, con un día de descanso; sin recargo suplementario si el promedio no excede el tope semanal.
- **Requisitos:** pacto entre empleador y trabajador. Forma escrita: **no verificado**.
- **Combina:** con la ordinaria como su variante de distribución. Con el art. 165: **no verificado**.
- Fuente: art. 161 (leyes.co) y `motor_horas_colombia_v2.md` §2.

### M3. Labores insalubres o peligrosas — art. 161 lit. b) CST
- **Aplica a:** labores declaradas insalubres o peligrosas.
- **Contenido:** reducción de la jornada máxima. Los topes concretos los fija la autoridad (Mintrabajo) caso a caso: **no verificado** (no se obtuvo cifra).
- **Requisitos:** declaratoria o autorización de la autoridad del trabajo: **no verificado** el procedimiento.
- **Combina:** **no verificado**.
- Fuente: art. 161 (leyes.co).

### M4. Menores de edad — art. 161 lit. c) CST
- **Aplica a:** menores de 18 años que pueden trabajar.
- **Contenido:** 15 a 17 años: máximo 6 h/día y 30 h/semana, solo en jornada diurna; mayores de 17 años: 8 h/día y 40 h/semana. La franja permitida según `motor_horas_colombia_v2.md` §2: hasta las 6:00 p.m. (15-17) y hasta las 8:00 p.m. (17-18). El texto actual del art. 161 lit. c) habla de «jornada diurna»; la hora de corte exacta debe cotejarse con el art. 114 del Código de Infancia y Adolescencia (**no verificado**).
- **Requisitos:** autorización de trabajo del menor (inspector del trabajo o ente territorial): **no verificado**.
- **Combina:** con ninguna otra extensión de jornada; los topes de menores son más restrictivos (inferencia, **no verificado** frente a fuente).
- Fuente: art. 161 (leyes.co); `motor_horas_colombia_v2.md` §2.

### M5. Turnos sucesivos sin solución de continuidad — art. 161 lit. d) CST
- **Aplica a:** empresas con labores que no pueden interrumpirse y que se cubren con turnos sucesivos.
- **Contenido:** **6 h diarias y 36 semanales**, sin recargo nocturno ni dominical (referencia: `motor_horas_colombia_v2.md` §2), con un día de descanso remunerado.
- **Requisitos:** que la labor se organice en turnos sucesivos sin solución de continuidad; pacto o forma escrita: **no verificado**. Opt-in contractual (marca de contrato).
- **Combina:** **no verificado**; no se combina con los recargos que elimina.
- Fuente: art. 161 (leyes.co).

### M6. Trabajo por turnos con promedio — art. 165 CST
- **Aplica a:** trabajo por turnos en labores que **no exigen actividad continua** (conceptos Mintrabajo).
- **Contenido:** la jornada diaria puede superar 8 h o la semanal 48 h (texto del artículo) si el **promedio en un periodo de hasta 3 semanas** no excede esos topes; no es trabajo suplementario.
- **Requisitos:** acuerdo entre las partes; labor sin actividad continua.
- **Combina:** con la ordinaria como su fundamento. Con M5 y M7: no combinan (M5 y M7 cubren labores continuas; inferencia, **no verificado**).
- **Ambigüedad conocida:** el «48» del artículo se lee en concordancia con el art. 161 (42 h hoy). Ver §8.
- Fuentes: https://leyonline.co/laws/codigo-sustantivo-del-trabajo/articulo-165-trabajo-por-turnos ; https://accounter.co/normatividad/conceptos/precisiones-sobre-distribucion-por-turnos-de-la-jornada-laboral ; Tribunal Superior de Bogotá, rad. 2021-513 (turnos 7x4 y 7x3 bajo el art. 165, promedio de 144 h en 3 semanas; no se verificó el detalle de la composición horaria de esos turnos).

### M7. Trabajo sin solución de continuidad — art. 166 CST
- **Aplica a:** labores que no admiten interrupción, atendidas con relevos.
- **Contenido:** hasta **56 h semanales**.
- **Requisitos:** que la labor sea sin solución de continuidad y exista relevo. Pacto: **no verificado**.
- **Combina:** **no verificado**. Precisión: `motor_horas_colombia_v2.md` §2 lo trata como «tope absoluto de 56 h sin generar extras».
- Fuente: https://leyes.co/codigo_sustantivo_del_trabajo/166.htm

### M8. Excluidos de la jornada máxima — art. 162 CST
- **Aplica a:** (lit. a) dirección, confianza o manejo; (lit. c) labores discontinuas o intermitentes y simple vigilancia, si residen en el sitio de trabajo; (lit. d) choferes de empresas de transporte (**vigencia discutida, no verificado**). El lit. b) (servicio doméstico) aparece vacío: lo derogó el art. 70 de la Ley 2466/2025 (`motor_horas_colombia_v2.md`; antes, C-372/1998 ya había declarado inexequible la exclusión de la jornada).
- **Contenido:** no hay tope de jornada máxima. Programación libre en horas; la regla de descanso subsiste (ver §4).
- **Requisitos:** la condición del cargo o de la labor, no un pacto de jornada. En vigilancia/discontinuas, residencia en el sitio.
- **Combina:** no aplica con M1-M7 mientras dure la exclusión (el tope no rige).
- Fuente: https://leyes.co/codigo_sustantivo_del_trabajo/162.htm ; `motor_horas_colombia_v2.md` §2.

**Otras figuras (fuera del catálogo mínimo, para no perderlas):** fuerza mayor (art. 163 CST, `motor_horas_colombia_v2.md` §2); sectores con régimen propio (seguridad, Ley 1920/2018; salud) — **no verificado** el detalle de su jornada.

---

## 3. Fichas de reglas medibles (CA-2)

Cada ficha: **tope diario / mínimo diario / horas semanales / periodo de medición / descanso y ventana / franja horaria / dos turnos el mismo día**. «n.a.» = la norma no lo regula (no es lo mismo que «sin límite»).

### F1. Ordinaria máxima (161 inc. 1)
- Tope diario: 8 h. Mínimo diario: n.a. Semanales: 42 h (tope; promedio no). Periodo: semana. Descanso: al menos 1 día por semana, con 24 h (arts. 161 y 172). Franja: n.a. para adultos. Dos turnos el mismo día: prohibido (161 par. 1).
- Periodo de medición «semana calendario»: la ley habla de semana; si el empleador define la semana de lunes a domingo o de otro modo: **no verificado**.

### F2. Flexible (161 lit. a)
- Tope diario: 9 h. Mínimo diario: 4 h. Semanales: promedio de 42 h (sin suplementario mientras el promedio no exceda). Periodo: semana (el artículo habla de «promedio»; la ventana exacta no se obtuvo: **no verificado**). Descanso: 1 día obligatorio; máximo 6 días de trabajo. Franja: n.a. Dos turnos: prohibido.

### F3. Insalubres o peligrosas (161 lit. b)
- Todas las cifras: **no verificado** (las fija la autoridad). Descanso: el general (172).

### F4. Menores (161 lit. c)
- Tope diario: 6 h (15-17) / 8 h (mayores de 17). Mínimo: n.a. Semanales: 30 h / 40 h (tope). Periodo: semana. Descanso: general (172). Franja: diurna; corte 6:00 p.m. / 8:00 p.m. según `motor_horas_colombia_v2.md` §2 (**no verificado** contra la fuente primaria). Dos turnos: prohibido (161 par. 1).

### F5. Turnos sucesivos (161 lit. d)
- Tope diario: 6 h. Mínimo: n.a. Semanales: 36 h (tope). Periodo: semana. Descanso: 1 día remunerado. Franja: n.a. (la nocturna y la dominical no generan recargo; ver `motor_horas_colombia_v2.md`). Dos turnos: prohibido (161 par. 1, con sus excepciones).

### F6. Turnos con promedio (165)
- Tope diario: sin tope fijo; puede superar 8 h si el promedio del periodo no las excede. Mínimo: n.a. Semanales: promedio ≤ tope (48 h del texto; 42 h por 161: ver §8). Periodo: **hasta 3 semanas**. Descanso: no se exime (172). Franja: n.a. Dos turnos: prohibido (161 par. 1). Un techo diario absoluto distinto de la jornada de 24 h: **no verificado**.

### F7. Sin solución de continuidad (166)
- Tope diario: n.a. (**no verificado**). Mínimo: n.a. Semanales: hasta 56 h. Periodo: semana. Descanso: general (172); su compensación en rotación: **no verificado**. Franja: n.a. Dos turnos: prohibido salvo excepción del par. 1 (**no verificado** su aplicación a 166).

### F8. Excluidos (162)
- Tope diario/semanal: no rige. Mínimo: n.a. Periodo: n.a. Descanso: no se exime (ver §4; **no verificado** que la exclusión no afecte el dominical para vigilancia residente). Franja: n.a. Dos turnos: el par. 1 del 161 exceptúa dirección, confianza y manejo.

### Tabla resumen modalidad x regla

| Modalidad | Tope diario | Mín. diario | Horas semanales | Periodo medición | Descanso | Franja | Dos turnos/día |
|---|---|---|---|---|---|---|---|
| M1 Ordinaria (161) | 8 h | n.a. | 42 h (tope) | Semana | ≥ 1 día, 24 h | n.a. | Prohibido |
| M2 Flexible (161 a) | 9 h | 4 h | Promedio 42 h | Semana (no verificado) | 1 día; ≤ 6 días de trabajo | n.a. | Prohibido |
| M3 Insalubres (161 b) | no verificado | no verificado | no verificado | no verificado | General | n.a. | Prohibido |
| M4 Menores (161 c) | 6 h / 8 h | n.a. | 30 h / 40 h | Semana | General | Diurna (corte no verificado) | Prohibido |
| M5 Sucesivos (161 d) | 6 h | n.a. | 36 h | Semana | 1 día remunerado | n.a. | Prohibido |
| M6 Turnos (165) | Puede > 8 h | n.a. | Promedio ≤ 48 h / 42 h | Hasta 3 semanas | General | n.a. | Prohibido |
| M7 Continuidad (166) | no verificado | n.a. | Hasta 56 h | Semana | General (no verificado) | n.a. | no verificado |
| M8 Excluidos (162) | No rige | n.a. | No rige | n.a. | No se exime | n.a. | Excepción dirección/confianza |

---

## 4. Descanso (CA-3)

**¿Existe una modalidad que exima del descanso? Según las fuentes consultadas, no.** Ninguna de las fichas M1-M8 suprime el descanso: M2 y M5 lo exigen expresamente; M8 excluye de la **jornada máxima**, no del descanso (la exclusión se lee solo sobre horas; confirmación con fuente específica: **no verificado**). El único candidato a «sin descansos» es la percepción de que quien no tiene jornada máxima no descansa; es incorrecta como regla general, pero la forma en que el descanso se compensa (otro día de la semana, en el lugar de trabajo para vigilantes residentes) debe confirmarse con el abogado. **Pregunta abierta para el experto:** ¿qué modalidad tenía en mente al decir «el empleado no necesita descansos»?

Reglas transversales a todas las modalidades:

1. **Descanso dominical remunerado de al menos 24 h** (art. 172 CST). https://leyes.co/codigo_sustantivo_del_trabajo/172.htm
2. **Prohibición de dos turnos el mismo día** (art. 161 par. 1), salvo dirección, confianza o manejo.
3. **Compensatorio por trabajo dominical habitual** (arts. 179-181 CST): quien labora habitualmente el domingo tiene derecho a un día de descanso compensatorio remunerado; ocasional: elegible entre compensatorio o remuneración. Detalle de habitualidad (SL3567-2019, sentencia 10079/1997): ver `motor_horas_colombia_v2.md` §1; no releído aquí.

Alcance: este documento solo trata la programación del descanso, no su remuneración ni recargos.

---

## 5. Efecto en la Jornada y en las advertencias (CA-4)

### 5.1 Lo que existe hoy

- **Jornada** (glosario «Jornada», `LimitesJornada`): cuatro parámetros: horas semanales (valor exacto), tope diario, mínimo diario (0 si no aplica) y días de descanso por semana (0-6; 0 = no se controla). «El sistema no impone modalidades legales ni certifica cumplimiento.»
- **Advertencias de plantilla** (`AuditoriaPlantillaTurnos`, `TipoAdvertenciaPlantilla`): hoy solo diarias — `PlantillaSinJornada`, `DiaSinTurno`, `SuperaTopeDiario`, `PorDebajoDelMinimoDiario`. Las cuatro semanales (`SuperaHorasSemanales`, `PorDebajoDeHorasSemanales`, `FaltanDiasDeDescanso`, `SobranDiasDeDescanso`) existen en el enum sin uso desde #1292.
- **Advertencias de la programación semanal** (`CalculadorAdvertencias`, semana lunes-domingo): semanales (las mismas cuatro) con `esJuzgable` (semana juzgable: por debajo y descansos solo se juzgan si la semana está completa; con ausencias, CA-ADR-0036 cuenta los días de ausencia en la medición) y diarias (`SuperaTopeDiario`, `PorDebajoDelMinimoDiario`, `PorDebajoDelDescansoEntreTurnos`, `SeSolapaConTurnoDeLaVispera`). El descanso entre turnos usa un mínimo fijo de 6 h (cobertura de marcación, CA-ADR-0042), que **no es** una regla de la ley sobre jornada.

### 5.2 Cobertura por modalidad

Leyenda: ✅ cubre bien, ◐ cubre con límites, ✖ no cubre.

| Modalidad | (a) Qué cubre hoy la Jornada | Qué falta | (b) Advertencias que cambian o nacen |
|---|---|---|---|
| M1 Ordinaria | Horas semanales 42, tope diario 8, descansos (✅) | Distribución en 5 o 6 días no se valida salvo con `DiasDescansoPorSemana` (◐); 24 h de descanso (no hay regla de 24 h continuas) | Plantilla: las semanales solo valen si el ciclo es múltiplo de 7 (ver 5.3). Programación: sin cambios |
| M2 Flexible | Tope diario 9, mínimo 4 (✅ con valores distintos); semanales 42 (◐: el sistema usa valor exacto, la flexible habla de promedio) | Máximo 6 días de trabajo (◐ por descansos ≥ 1); «sin recargo suplementario por promedio»: la clasificación es del motor | `SuperaHorasSemanales` ya aplica por semana; con flexible, una semana con 45 h no es exceso si el promedio cumple: nace «periodo de promedio» |
| M3 Insalubres | Se puede aproximar con valores propios (✅ si se conocen) | Cifras y autorización: no verificado | Ninguna nueva; depende de las cifras |
| M4 Menores | Horas, tope diario (✅) | **Franja horaria permitida** (no es un parámetro de la Jornada) (✖) | Nace advertencia de franja («fuera de franja permitida») en plantilla y programación |
| M5 Sucesivos | 6 h / 36 h (✅) | Descanso remunerado 1 día (✅ vía descansos=1) | Ninguna nueva (los recargos son del motor, no de la programación) |
| M6 Turnos 165 | Tope diario y semanales no expresan «promedio en hasta 3 semanas» (✖) | **Periodo de medición** configurable; tope diario que pueda exceder 8 h | Plantilla: nace la advertencia de promedio por periodo/ciclo. Programación: la medición de 3 semanas rebasa la semana lunes-domingo, y con ausencias (CA-ADR-0036) hay que decidir si un día de ausencia cuenta como horas del promedio |
| M7 Continuidad | Horas 56 (✅ como valor exacto: ◐, la ley da un techo, no «exacto») | Descansos con rotación | `SuperaHorasSemanales` aplica como tope; `PorDebajoDeHorasSemanales` deja de tener sentido (es techo, no valor exacto) |
| M8 Excluidos | Se aproximaría con valores holgados (◐: el sistema no puede decir «sin tope») | Representación de «no aplica el tope» | Se suprimen `SuperaTopeDiario`, `SuperaHorasSemanales`; el descanso se sigue verificando |

Nota sobre el valor exacto: para la mayoría de las modalidades, la ley da un **techo** y la Jornada vigente trata las horas semanales como valor **exacto** (más = exceso, menos = subprogramación). Esa diferencia es transversal (ver §8).

### 5.3 Caso trabajado: 6x2 de 8 h contra 42 h

Ciclo 6x2: 6 días de 8 h seguidos de 2 de descanso; ciclo de 8 días = 48 h de trabajo; promedio por día de ciclo = 6 h; promedio semanal = 6 h × 7 = **42 h**. Ajusta exactamente a 42 h en el promedio.

- **Semana calendario lunes-domingo (programación):** como el ciclo es de 8 días y la semana de 7, cada semana contiene 5 o 6 días de trabajo, es decir **40 h o 48 h** (el patrón se desplaza un día por semana). Contra una Jornada de 42 h exacta, `CalculadorAdvertencias` emite en la semana de 48 h `SuperaHorasSemanales` (360 min) y en la de 40 h `PorDebajoDeHorasSemanales` (120 min); y con `DiasDescansoPorSemana` = 1 o 2, emitiría `FaltanDiasDeDescanso`/`SobranDiasDeDescanso` alternadas. Las advertencias son **falsas** frente al diseño legalmente razonable: el diseño de 42 h promedio las dispararía todo el año.
- **Plantilla (ciclo de N días):** con N = 8 las advertencias semanales no tienen definición (no hay «semana»); por eso, desde #1292, la plantilla solo emite diarias. Un 6x2 de 8 h pasa limpio las diarias (tope 8, mínimo ≤ 8).
- **Cumplimiento legal:** 6x2 no está nombrado en la ley. Su encaje más probable es el art. 165 (turnos con promedio de hasta 3 semanas) si hay acuerdo y la labor no es continua, o el art. 166/161 lit. d) si la labor es continua. En el periodo de 3 semanas (21 días) los días trabajados varían (15 o 16 de 21 en la rotación de 8 días), por lo que **el promedio de 3 semanas no es constante** y, en la práctica, la medición sensata es sobre el ciclo completo o sobre un múltiplo común (56 días = 8 semanas = 7 ciclos, 336 h = 42 h/semana). Si la ley acepta un periodo mayor a 3 semanas para este caso: **no verificado**. 
- **Lo que haría falta:** un periodo de medición distinto de la semana (el ciclo o hasta 3 semanas) y la advertencia de promedio contra ese periodo; la semana lunes-domingo dejaría de usarse como unidad de juicio para esa modalidad.

---

## 6. Resumen de huecos de la Jornada

1. **Periodo de medición:** no existe; la única unidad es la semana lunes-domingo (M2, M6, M7 y todo ciclo no múltiplo de 7).
2. **Techo vs valor exacto:** las horas semanales son exactas; la ley da topes o promedios (M1, M4, M5, M6, M7).
3. **Franja horaria permitida:** no existe (M4).
4. **Tope diario variable por periodo:** M6 permite superar 8 h con promedio.
5. **Descanso de 24 h continuas:** solo existe el conteo de días de descanso por semana.
6. **Representación de «no aplica» (M8):** 0 solo existe para descansos y mínimo diario.
7. **Marca de modalidad / pacto:** no hay dónde registrar el pacto, la autorización o la naturaleza de la labor (M2, M3, M5, M6, M7).

---

## 7. Fuentes

- CST arts. 161, 162, 165, 166, 172, 179-181 (leyes.co, leyonline.co).
- Ley 2101/2021 art. 3; Ley 2466/2025 arts. 10-14, 70; Circular Mintrabajo 0101/2025 (citada vía `motor_horas_colombia_v2.md`).
- Conceptos Mintrabajo sobre turnos: https://accounter.co/normatividad/conceptos/precisiones-sobre-distribucion-por-turnos-de-la-jornada-laboral
- Tribunal Superior de Bogotá, rad. 2021-513: https://portalhistorico.ramajudicial.gov.co/documents/35753502/83891604/SENTENCIA+2021-513-01+TRABAJO+POR+TURNO+%281%29.pdf/462e1d0f-1554-4c51-9ffd-95a3aa36e135
- Corte Constitucional C-372/1998 (vía `motor_horas_colombia_v2.md`).
- Código internos: `docs/ddd/ubiquitous-language.yaml` («Jornada», advertencias), `AuditoriaPlantillaTurnos.cs`, `TipoAdvertenciaPlantilla.cs`, `Advertencias.cs`; CA-ADR-0034, CA-ADR-0036.

---

## 8. Puntos para decidir con el experto (CA-6)

Esta sección **no decide nada**.

1. **Tope del art. 165:** el texto dice 48 h semanales; el art. 161 vigente dice 42 h. ¿Se juzga el promedio contra 48 o contra 42? ¿Y el tope diario del promedio?
2. **Nombre del concepto:** «modalidad de jornada» es provisional. «Modalidad» ya nombró a la plantilla semanal histórica; `motor_horas_colombia_v2.md` dice «régimen». ¿Cuál es el término del ubiquitous language?
3. **Tensión con el principio de la Jornada:** hoy la Jornada «no impone modalidades legales ni certifica cumplimiento». Si el sistema registra la modalidad y arma advertencias, ¿ese principio se enmienda o se reemplaza? ¿El sistema certifica o solo advierte?
4. **Convivencia:** ¿la modalidad legal convive con una Jornada pactada libre como la de hoy (Jornada sin modalidad), o toda Jornada nace con modalidad?
5. **Dónde se asigna:** ¿en la Jornada (catálogo) o en la Vinculación del colaborador? Hoy la Jornada se asigna a la Vinculación y se estampa al programar.
6. **«Sin descansos»:** ¿qué modalidad tenía en mente el experto? (§4: ninguna exime del descanso según las fuentes consultadas.)
7. **Techo vs valor exacto** de las horas semanales (§5.2).
8. **Ausencias en periodos largos:** ¿cuenta un día de ausencia como horas del periodo de promedio (CA-ADR-0036)?
9. **Ciclos que no encajan** (6x2): ¿se mide por ciclo, por periodo de hasta 3 semanas, o se exige un múltiplo de 7?
10. **Verificaciones pendientes** marcadas «no verificado» en este documento: cifras de M3, franja de M4, combinaciones entre modalidades, forma del pacto, alcance de la exclusión del art. 162 sobre el descanso.
