# Motor de conteo de horas laborales en Colombia, abril 2026

**Documento técnico-legal para el desarrollo de un motor de clasificación temporal de horas trabajadas.** A abril de 2026 rige en Colombia una matriz compleja que integra tres capas: el Código Sustantivo del Trabajo (CST) original, la **Ley 2101 de 2021** (reducción gradual de jornada, con tope vigente de **44 horas semanales** hasta el 14 de julio de 2026) y la **Ley 2466 de 2025** (reforma laboral, sancionada el **25 de junio de 2025**, Diario Oficial 53.160). Esta última produjo dos cambios estructurales ya vigentes en abril de 2026: el **horario nocturno se adelantó a las 7:00 p.m.** (rige desde el **25 de diciembre de 2025**) y el **recargo dominical/festivo está en 80%** (en camino escalonado hasta 100% en julio de 2027). El motor debe, por cada minuto registrado entre la entrada y la salida reales de un trabajador, asignar una y solo una categoría legal, contrastando el registro real contra la jornada programada y contra los rangos diurno/nocturno, los días calendario y los días de descanso obligatorio. Este documento entrega el marco normativo, las reglas de clasificación con artículo-fuente y ejemplos concretos, y marca explícitamente los puntos de ambigüedad que requieren validación con abogado laboralista.

> ⚠️ **Advertencia de alcance.** Este documento clasifica y cuenta horas; no calcula valores monetarios. Los porcentajes citados (35%, 25%, 75%, 80%) son etiquetas jurídicas de la categoría, no fórmulas de liquidación. La implementación de reglas con efecto económico debe ser revisada por un abogado laboralista colombiano antes de producción.

---

## 1. Marco legal vigente a abril de 2026

Las fuentes normativas que el motor debe tratar como autoridad primaria son, en este orden de jerarquía interpretativa: la Constitución Política (art. 53, principio de primacía de la realidad e irrenunciabilidad de derechos mínimos), el **Código Sustantivo del Trabajo, arts. 158 a 181**, la **Ley 2101 de 2021** (publicada en Diario Oficial 51.736 del 15 de julio de 2021), la **Ley 2466 de 2025** (Diario Oficial 53.160 del 25 de junio de 2025), la **Ley 51 de 1983** (Ley Emiliani, festivos y traslado al lunes) y los decretos reglamentarios vigentes (Decreto Único 1072 de 2015 en lo compatible, Resolución 2404 de 2019 del Ministerio del Trabajo en materia de riesgo psicosocial). Como autoridad interpretativa, el motor debe integrar la **Circular Externa 0101 del 22 de septiembre de 2025** del Ministerio del Trabajo (lineamientos sobre jornada, horas extras y recargos tras la Ley 2466) y la **Circular 0102 de 2025** (archivo de trámites de autorización de horas extras, hoy eliminados). La jurisprudencia aplicable proviene de la Sala de Casación Laboral de la Corte Suprema de Justicia (SL1514-2023 sobre disponibilidad; SL3567-2019 y sentencia 10079 del 11 de diciembre de 1997 sobre habitualidad dominical) y de la Corte Constitucional (C-372/1998 sobre servicio doméstico; C-331/2023 sobre derechos de directivos).

La tabla siguiente condensa las **fechas exactas de entrada en vigencia** de cada cambio relevante para el motor:

| Disposición | Fuente | Vigencia | Estado en abril 2026 |
|---|---|---|---|
| Jornada semanal 44 h | Ley 2101/2021 art. 3, tercera reducción | 15 de julio de 2025 | **Vigente** |
| Jornada semanal 42 h | Ley 2101/2021 art. 3, cuarta reducción + Ley 2466/2025 art. 11 | 15 de julio de 2026 | **Aún no vigente** (rige después) |
| Jornada nocturna 7:00 p.m. – 6:00 a.m. | Ley 2466/2025 art. 10 (modifica art. 160 CST), parágrafo 2 | 25 de diciembre de 2025 (6 meses post-sanción) | **Vigente** |
| Recargo dominical 80% | Ley 2466/2025 art. 14 (modifica art. 179 CST), parágrafo transitorio | 1 de julio de 2025 | **Vigente** |
| Recargo dominical 90% | Ley 2466/2025 art. 14 parágrafo transitorio | 1 de julio de 2026 | **Aún no vigente** |
| Recargo dominical 100% pleno | Ley 2466/2025 art. 14 texto principal | 1 de julio de 2027 | **Aún no vigente** |
| Eliminación de autorización previa del Mintrabajo para horas extras | Ley 2466/2025 art. 12 (modifica art. 162 num. 2 CST) | 25 de junio de 2025 | **Vigente** |
| Límite 2 h diarias / 12 h semanales de extras | Ley 2466/2025 art. 13 (nuevo art. 167A CST) | 25 de junio de 2025 | **Vigente** |
| Servicio doméstico dentro de jornada máxima | Ley 2466/2025 art. 70 (deroga literal b art. 162 CST) | 25 de junio de 2025 | **Vigente** |

---

## 2. Jornada ordinaria vigente y rangos horarios de aplicación

A abril de 2026 la **jornada máxima ordinaria es de 44 horas semanales**, distribuibles de común acuerdo entre empleador y trabajador en **5 o 6 días**, garantizando siempre el día de descanso, con un **máximo diario de 8 horas** (art. 161 CST, tras la redacción introducida por el art. 11 de la Ley 2466 de 2025, que integró la gradualidad de la Ley 2101 de 2021). El empleador puede anticipar voluntariamente las 42 horas; el cronograma legal solo fija el **techo máximo**. La reducción no puede implicar disminución de salario, de prestaciones ni del valor de la hora ordinaria (art. 4 Ley 2101/2021).

La **franja diurna** es de **6:00 a.m. a 7:00 p.m.** (hasta las 18:59:59), y la **franja nocturna** es de **7:00 p.m. a 6:00 a.m. del día siguiente**, conforme al nuevo art. 160 CST introducido por el art. 10 de la Ley 2466 de 2025 y vigente desde el **25 de diciembre de 2025**. Antes de esa fecha regía el corte a las 9:00 p.m. heredado de la Ley 789 de 2002; el motor debe aplicar esta nueva franja a todo registro cuya fecha sea igual o posterior al 25 de diciembre de 2025.

**Jornadas especiales del art. 161 CST** que el motor debe reconocer como regímenes distintos del general:

- **Jornada flexible (art. 161 literal a)**: las partes pueden pactar que la jornada semanal de 42 horas (44 en transición) se distribuya en **mínimo 4 y máximo 9 horas diarias continuas**, en máximo 6 días con un día de descanso obligatorio, **sin recargo por trabajo suplementario** mientras el promedio no exceda el tope semanal. Es decir, una jornada de 9 horas un martes no genera extras si el promedio semanal se respeta.
- **Turnos sucesivos sin solución de continuidad (art. 161 literal d)**: 6 horas diarias y 36 semanales, sin recargo nocturno ni dominical, con un día de descanso remunerado. Es un régimen opt-in que el motor debe marcar con flag contractual.
- **Trabajo por turnos del art. 165 CST**: la jornada puede superar 8 h diarias o 44 h semanales siempre que el **promedio de 3 semanas** no exceda esos topes; esta ampliación no constituye trabajo suplementario.
- **Sin solución de continuidad (art. 166 CST)**: tope absoluto de **56 h semanales** sin generar extras.
- **Fuerza mayor (art. 163 CST)**: permite exceder la jornada sin autorización y sin los topes ordinarios en emergencias; exige registro.
- **Menores de edad (art. 161 lit. c)**: 15-17 años, máximo 6 h/día y 30 h/semana, solo hasta las 6:00 p.m.; 17-18 años, máximo 8 h/día y 40 h/semana, solo hasta las 8:00 p.m.

**Servicio doméstico**: el art. 70 de la Ley 2466 derogó el literal b) del art. 162 CST que los excluía de la jornada máxima. **Desde el 25 de junio de 2025 aplica el régimen general** (44 h vigentes en abril 2026, 42 h desde julio 2026), con todos los recargos y extras. La Corte Constitucional ya había declarado inexequible en la Sentencia C-372/1998 la exclusión respecto del límite de jornada; la Ley 2466 cerró formalmente la ambigüedad.

**Excepciones del art. 162 CST que permanecen vigentes**: trabajadores de dirección, confianza o manejo (literal a), y labores discontinuas, intermitentes o de simple vigilancia cuando el trabajador **resida en el lugar de trabajo** (literal c). Sectores de seguridad (Ley 1920 de 2018) y salud tienen regímenes propios fuera del tope de 2 h/12 h de extras.

---

## 3. Categorías de horas y reglas de clasificación

El motor debe clasificar cada minuto efectivamente trabajado en una y solo una de las ocho categorías siguientes. La clasificación depende de tres ejes independientes que se combinan: (i) **día calendario** (hábil versus domingo/festivo, evaluado según el reloj de pared 00:00–24:00 del día); (ii) **franja horaria** (diurna 6:00 a.m.–7:00 p.m. versus nocturna 7:00 p.m.–6:00 a.m.); y (iii) **posición respecto de la jornada programada** (dentro versus fuera, siendo "fuera" sinónimo de suplementario o extra).

La siguiente tabla es la **tabla de referencia rápida** que el motor debe usar como matriz de decisión final:

| # | Categoría | Franja horaria | Día | Posición | Artículo-fuente | Etiqueta de recargo (solo referencia) |
|---|---|---|---|---|---|---|
| 1 | Hora ordinaria diurna | 6:00 a.m. – 7:00 p.m. | Hábil (no domingo ni festivo) | Dentro de jornada programada | Arts. 158 y 161 CST | 0% (salario ordinario) |
| 2 | Hora ordinaria nocturna (con recargo nocturno) | 7:00 p.m. – 6:00 a.m. | Hábil | Dentro de jornada programada | Art. 168 num. 1 CST | 35% |
| 3 | Hora extra diurna | 6:00 a.m. – 7:00 p.m. | Hábil | Fuera de jornada programada | Arts. 159 y 168 num. 2 CST | 25% |
| 4 | Hora extra nocturna | 7:00 p.m. – 6:00 a.m. | Hábil | Fuera de jornada programada | Arts. 159 y 168 num. 3 CST | 75% |
| 5 | Hora dominical/festiva ordinaria diurna | 6:00 a.m. – 7:00 p.m. | Domingo o festivo | Dentro de jornada programada | Art. 179 CST (Ley 2466/2025 art. 14) | 80% |
| 6 | Hora dominical/festiva ordinaria nocturna | 7:00 p.m. – 6:00 a.m. | Domingo o festivo | Dentro de jornada programada | Arts. 168 + 179 CST | 80% + 35% = 115% (acumulación aditiva) |
| 7 | Hora extra diurna en dominical/festivo | 6:00 a.m. – 7:00 p.m. | Domingo o festivo | Fuera de jornada programada | Arts. 168 num. 2 + 179 CST | 25% + 80% = 105% |
| 8 | Hora extra nocturna en dominical/festivo | 7:00 p.m. – 6:00 a.m. | Domingo o festivo | Fuera de jornada programada | Arts. 168 num. 3 + 179 CST | 75% + 80% = 155% |

**Regla de acumulación** (Circular 0101/2025 Mintrabajo, interpretando art. 168 num. 4 CST): los recargos **se suman aditivamente, no en cascada**. Es decir, una hora extra nocturna en festivo es 25% + 35% + 80% = nunca se multiplica. El art. 168 num. 4 CST dice que los recargos "se producen de manera exclusiva, es decir, sin acumularlo con algún otro", lo cual la doctrina interpreta como **no duplicación** dentro del mismo eje (no se suma "extra" con "extra"), pero sí admite combinar ejes distintos (nocturno + dominical + extra), que son categorías independientes. Esta interpretación es la pacífica y la recogida por el Ministerio del Trabajo en la Circular 0101/2025.

**Cruces de frontera dentro de una misma jornada.** El motor debe segmentar el periodo trabajado en intervalos homogéneos cada vez que cambie alguno de los tres ejes (día, franja, posición). Cada intervalo se clasifica en una sola categoría. Los tres cruces típicos son:

- **Cruce diurno↔nocturno a las 7:00 p.m.**: un turno programado de 2:00 p.m. a 9:00 p.m. produce 5 horas ordinarias diurnas (2:00 p.m.–7:00 p.m.) y 2 horas ordinarias nocturnas con recargo del 35% (7:00 p.m.–9:00 p.m.).
- **Cruce nocturno↔diurno a las 6:00 a.m.**: un turno de 10:00 p.m. a 7:00 a.m. produce 8 horas nocturnas (10:00 p.m.–6:00 a.m.) y 1 hora diurna (6:00 a.m.–7:00 a.m.).
- **Cruce de día calendario a las 00:00**: relevante principalmente para la transición hábil→domingo o hábil→festivo, tratada en la sección 6.

---

## 4. Reglas de conteo y tratamiento de desviaciones respecto al turno planificado

**Tiempo efectivo versus tiempo de presencia.** Solo se cuenta como jornada el tiempo en que el trabajador **presta efectivamente el servicio o permanece a disposición del empleador bajo subordinación**. El art. 167 CST establece que el descanso intermedio (almuerzo, cena, descansos entre secciones) **no se computa en la jornada**; la Sala Laboral de la Corte Suprema, en radicación 10659 citada por el Concepto 142251 de 2021 de Función Pública, ratifica que pueden descontarse del lapso de jornada todos los descansos obligatorios y convenidos, computándose solo el trabajo neto. La excepción es cuando durante el descanso el trabajador **permanece a disposición** (no puede abandonar el puesto, debe atender llamadas): ese tiempo sí es jornada.

**Pausas activas.** A diferencia del almuerzo, las pausas activas (fundamento en art. 5 parágrafo de la Ley 1355 de 2009, protocolos del SG-SST, y guías de la Resolución 2404 de 2019 del Mintrabajo) son breves interrupciones dispuestas por el empleador en cumplimiento de una obligación legal de prevención; durante ellas el trabajador permanece a disposición, por lo que **sí se computan dentro de la jornada ordinaria y son remuneradas**. El motor debe tratarlas como tiempo trabajado.

**Llegadas tarde y salidas tempranas.** El salario se causa por el servicio efectivamente prestado; el tiempo no trabajado puede **descontarse proporcionalmente**, lo cual no es sanción disciplinaria sino ajuste salarial por servicio no prestado (art. 57 num. 4 CST en relación con arts. 58 y 60 CST). Si el empleador quiere imponer además una **multa**, debe estar prevista en el **reglamento interno de trabajo**, no puede exceder la quinta parte del salario de un día, y los recaudos se destinan a premios para los trabajadores, no al empleador (art. 113 CST). Para el motor: el tiempo entre la hora programada de inicio y la hora real de entrada, cuando la entrada es posterior, **no genera hora de ninguna categoría** (el trabajador no prestó servicio); el sistema debe registrar ese segmento como "no trabajado" para control y eventual descuento, pero sin etiquetarlo en ninguna de las ocho categorías.

**Llegadas tempranas y salidas tardías (horas extras tácitas).** El trabajo suplementario **no es automático**: requiere orden o autorización del empleador, expresa o tácita. La Corte Constitucional (Sentencia T-326 de 1994) sostuvo que la labor suplementaria no hace parte del núcleo esencial del derecho al trabajo y que corresponde al empleador autorizarla. Sin embargo, por aplicación del principio de **primacía de la realidad (art. 53 CP)** y de la jurisprudencia reiterada de la Sala Laboral, cuando el empleador **conoce y tolera** que el trabajador permanezca laborando más allá de su jornada, hay **autorización tácita** y las horas deben pagarse con su recargo. La mera permanencia voluntaria del trabajador sin necesidad del servicio y sin conocimiento del empleador **no genera extras**. La Ley 2466/2025 (art. 12) eliminó la autorización previa del Mintrabajo pero impuso al empleador la obligación de llevar **registro diario del trabajo suplementario** (nombre, actividad, horas, diurnas/nocturnas) y entregar soporte al trabajador. Para el motor se recomienda exigir un **flag de autorización** por turno (expresa o tácita-por-omisión) que gobierne si el tiempo por fuera de la jornada se etiqueta como extra o como "no autorizado" (este último debe alertar al PM porque legalmente **sí debe pagarse igualmente** cuando hubo conocimiento del empleador; ver sección 5).

**Fracciones de hora.** No existe en Colombia norma legal que regule el redondeo de fracciones de minuto. El motor debe **computar proporcionalmente al tiempo efectivamente trabajado** (p. ej., 37 minutos extra = 37/60 de hora extra con su recargo), en aplicación de los principios de proporcionalidad (arts. 127 y 132 CST), favorabilidad (art. 21 CST) y primacía de la realidad. Cualquier política de redondeo del empleador (p. ej., gracia de 5 minutos, redondeo al cuarto de hora) debe constar en el reglamento interno de trabajo y **no puede desmejorar los mínimos legales** (art. 13 CST, irrenunciabilidad). La recomendación técnica es operar en precisión de minuto y diferir cualquier redondeo al momento de liquidación con bandera de auditoría.

**Trabajador que no alcanza su jornada (salida temprana autorizada).** Si la salida temprana es por orden o consentimiento del empleador, el art. 140 CST establece que se causa el salario (tiempo a disposición no imputable al trabajador). Si es por justa causa del trabajador (accidente, enfermedad, calamidad doméstica, fuerza mayor, caso fortuito), aplican los arts. 173 num. 2 y 57 num. 9 CST. Si es injustificada, se descuenta. El motor debe exigir un motivo de la salida temprana para decidir si el tiempo faltante se computa como trabajado, se descuenta o queda pendiente de resolución.

---

## 5. Límites legales de horas extras y consecuencias del exceso

El **art. 167A CST**, introducido por el art. 13 de la Ley 2466 de 2025, establece: *"En ningún caso las horas extras de trabajo, diurnas o nocturnas, podrán exceder de dos (2) horas diarias y doce (12) semanales. PARÁGRAFO. Se exceptúa de la aplicación de la presente disposición al sector de seguridad, de conformidad con la Ley 1920 de 2018 y sus decretos reglamentarios, y al sector salud, conforme a la normatividad vigente."*

El tope total combinado a abril de 2026 es, conforme a la Circular 0101/2025: **44 horas ordinarias + 12 horas extras = 56 horas semanales máximas**. Desde el 15 de julio de 2026 será 42 + 12 = **54 horas semanales**. La autorización previa del Ministerio del Trabajo **fue eliminada** por el art. 12 de la Ley 2466 (nuevo texto del art. 162 num. 2 CST); ahora el empleador solo debe llevar el registro detallado y entregar soporte al trabajador.

**Qué pasa si se excede el límite.** La doctrina mayoritaria (Actualícese, Gus Abogados) y el principio de **irrenunciabilidad de derechos laborales (art. 53 CP y art. 14 CST)** coinciden en que las horas efectivamente trabajadas **deben pagarse con sus recargos correspondientes**, independientemente de que el empleador haya violado el tope. La consecuencia recae sobre el empleador, no sobre el trabajador: el Ministerio del Trabajo puede **suspender hasta por seis meses la facultad del empleador de ordenar trabajo suplementario**, además de imponer multas administrativas (parágrafo del art. 162 num. 2 CST, tras Ley 2466). La Sala Laboral de la Corte Suprema ha reiterado en fallos sobre disponibilidad (SL5584-2017, SL4883-2020, SL1514-2023) que el hecho objetivo del trabajo bajo subordinación es determinante, y que la carga de la prueba del pago recae en el empleador.

**Implicación crítica para el motor.** El sistema **no debe rechazar ni eliminar** las horas registradas que excedan el tope de 2 diarias o 12 semanales: debe clasificarlas en la categoría 3, 4, 7 u 8 que corresponda y marcarlas con una **alerta de "exceso legal"** para el empleador, sin alterar el conteo. Rechazarlas expondría al desarrollador y a la empresa usuaria a responsabilidad por no reconocimiento de derechos laborales.

> ⚠️ **Zona de interpretación divergente.** Algunas firmas laboralistas sostienen que las horas trabajadas en exceso del tope pueden considerarse "tolerancia de facto" y pagarse sin que el empleador quede exento de sanción. Otros autores defienden que el tope es absoluto y que pagarlas normaliza la infracción. La jurisprudencia colombiana no tiene una sentencia emblemática única con radicación específica que resuelva el punto; se articula sobre los arts. 53 CP, 13, 14 y 159 CST y sobre fallos sobre disponibilidad. **Se recomienda consulta específica con abogado laboralista antes de implementar lógica de rechazo de horas.**

---

## 6. Cruces temporales y reglas de segmentación

**Definición operativa de "domingo" y "festivo".** La doctrina pacífica colombiana aplica el **criterio calendario**: el domingo y el festivo comienzan a las **00:00** (medianoche que marca el inicio del día) y terminan a las **24:00**. No existe un "criterio de jornada" que extienda el día de descanso hasta el final del turno. Este criterio se deduce del art. 172 CST (descanso mínimo de 24 horas), del parágrafo 1 del art. 179 CST reformado por Ley 2466 ("dos días durante el mes calendario"), y es ratificado uniformemente por fuentes doctrinarias (Gerencie, Loggro, Bitakora) y por la aplicación implícita en conceptos del Mintrabajo (Concepto 7105 de 2018; Concepto 48572 de 2023).

> ⚠️ **Ambigüedad menor.** El Ministerio del Trabajo no utiliza la expresión literal "criterio calendario" en sus conceptos; la doctrina es pacífica pero no hay sentencia exacta de la Sala Laboral con radicación que lo enuncie textualmente. El motor debe aplicar el criterio calendario por ser el estándar aceptado, marcándolo como supuesto configurable.

**Cruce sábado→domingo (jornada nocturna a caballo del calendario).** Para un turno programado de **sábado 10:00 p.m. a domingo 6:00 a.m.** (8 horas), la clasificación es: **sábado 10:00 p.m.–11:59 p.m.** (2 horas) son horas ordinarias nocturnas de día hábil (categoría 2, recargo 35%); **domingo 00:00–6:00 a.m.** (6 horas) son horas ordinarias nocturnas dominicales (categoría 6, recargo aditivo 80% + 35% = 115%). Cada segmento se cuenta de forma independiente por aplicación estricta del criterio calendario.

**Cruce domingo→lunes.** Simétricamente, un turno **domingo 10:00 p.m. a lunes 6:00 a.m.**: 2 horas dominicales nocturnas (categoría 6) y 6 horas ordinarias nocturnas de día hábil (categoría 2).

**Cruce hábil→festivo.** Idéntica lógica: el festivo inicia a las 00:00 de su día calendario y se comporta igual que un domingo para efectos de recargo.

**Festivo que cae en domingo.** El **art. 179 num. 2 CST** (con la redacción de la Ley 2466) dispone que *"si con el día de descanso obligatorio coincide otro día de descanso remunerado, solo tendrá derecho el trabajador, si trabaja, al recargo establecido en el numeral anterior"*. Es decir, **no se suman ni duplican** los dos recargos: se paga un solo 80% (en abril 2026). Para los festivos "trasladables" de la Ley 51 de 1983 (seis de enero, 19 de marzo, 29 de junio, 15 de agosto, 12 de octubre, 1 de noviembre, 11 de noviembre, Ascensión del Señor, Corpus Christi, Sagrado Corazón de Jesús), si caen en domingo **se trasladan al lunes siguiente**, y ese lunes es el día festivo con descanso remunerado. Los festivos **fijos** (1 de enero, 1 de mayo, 20 de julio, 7 de agosto, 8 de diciembre, 25 de diciembre, Jueves Santo, Viernes Santo) no se trasladan y, si caen en domingo, simplemente se aplica el recargo único del art. 179 num. 2.

**Jornada programada que cruza medianoche.** El motor debe tratar la jornada programada como un **intervalo absoluto [inicio, fin]**, no como un rango horario reloj. Si la programación es "20:00 a 04:00 del día siguiente", el tiempo real dentro de ese intervalo es "dentro de jornada programada" (categorías 1, 2, 5 o 6). El tiempo anterior a las 20:00 real o posterior a las 04:00 real, si el trabajador estuvo presente, es suplementario (categorías 3, 4, 7 u 8), sujeto a la regla de autorización de la sección 4.

---

## 7. Dominicales, festivos y modalidades de trabajo en día de descanso

Los **18 festivos nacionales** vigentes en Colombia (Ley 51 de 1983, art. 1, en la redacción integrada con el art. 177 CST) se clasifican así:

**Fijos (no se trasladan):** 1 de enero (Año Nuevo), 1 de mayo (Día del Trabajo), 20 de julio (Independencia), 7 de agosto (Batalla de Boyacá), 8 de diciembre (Inmaculada Concepción), 25 de diciembre (Navidad), Jueves Santo y Viernes Santo.

**Trasladables al lunes siguiente (Ley Emiliani):** 6 de enero, 19 de marzo, 29 de junio, 15 de agosto, 12 de octubre, 1 de noviembre, 11 de noviembre, Ascensión del Señor, Corpus Christi y Sagrado Corazón de Jesús. La regla del art. 1 inciso 2 de la Ley 51/1983 es: *"Cuando las mencionadas festividades caigan en domingo el descanso remunerado igualmente se trasladará al lunes"*.

**Trabajo habitual versus ocasional (parágrafo 1 del art. 179 CST, texto Ley 2466).** El trabajo en día de descanso obligatorio es **ocasional** cuando el trabajador labora **hasta dos** días de descanso obligatorio durante el mes calendario; es **habitual** cuando labora **tres o más**. La jurisprudencia (CSJ Sala Laboral, sentencia 10079 del 11 de diciembre de 1997; CSJ SL3567-2019) suma domingos y festivos para determinar la habitualidad. La consecuencia en el **descanso compensatorio** es: en trabajo **habitual**, el trabajador tiene derecho **acumulativo** a recargo económico **más** descanso compensatorio remunerado (arts. 179 y 181 CST); en trabajo **ocasional**, el trabajador **elige** entre recargo en dinero o descanso compensatorio remunerado (arts. 179 y 180 CST). Para el motor, el descanso compensatorio **no altera la clasificación temporal** de las horas trabajadas en el día de descanso (que siguen siendo categorías 5, 6, 7 u 8): solo es un efecto adicional sobre la agenda semanal siguiente.

**Parágrafo 3 del art. 179 CST (Ley 2466).** Las partes pueden pactar por escrito que el día de descanso obligatorio sea **distinto al domingo**; de no pactarse, se presume el domingo. Para el motor, esto implica que el "día de descanso obligatorio" es un dato contractual por trabajador, no una constante del sistema. Si el pacto fija el miércoles como día de descanso, el miércoles funciona igual que un domingo (categorías 5, 6, 7, 8) y el domingo pasa a ser un día hábil ordinario (categorías 1, 2, 3, 4), salvo que coincida con festivo.

> ⚠️ **Zona crítica de interpretación.** Un análisis del Departamento de Derecho Laboral de la Universidad Externado ("El desvanecimiento del recargo dominical en la reforma laboral") advierte que el parágrafo 3 puede "vaciar de contenido" el recargo dominical en sectores 24/7 si el empleador pacta unilateralmente el descanso entre semana. Esta controversia aún no tiene pronunciamiento de la Corte Constitucional. **El motor debe permitir parametrizar el día de descanso por contrato, pero alertar al PM sobre el riesgo de controversia legal en pactos unilaterales.**

---

## 8. Ejemplos concretos de clasificación

Los ejemplos asumen que todos los registros son posteriores al 25 de diciembre de 2025 (horario nocturno nuevo en vigor) y anteriores al 15 de julio de 2026 (tope semanal de 44 horas, recargo dominical de 80%).

**Ejemplo 1. Jornada diurna típica con salida tardía autorizada.** Jornada programada lunes a viernes 8:00 a.m.–5:00 p.m. (1 hora de almuerzo 12:00–1:00 p.m., no computa). Martes, entrada real 8:00 a.m., salida real 7:30 p.m., con orden expresa de horas extras. Clasificación: 8:00 a.m.–12:00 p.m. y 1:00 p.m.–5:00 p.m. = **8 horas ordinarias diurnas** (categoría 1); 5:00 p.m.–7:00 p.m. = **2 horas extras diurnas** (categoría 3); 7:00 p.m.–7:30 p.m. = **0,5 horas extras nocturnas** (categoría 4). Total día: 10,5 horas. Alerta: ese día está en el límite exacto del tope de 2,5 horas extras, lo cual ya supera el máximo diario de 2 horas del art. 167A CST (la fracción 0,5 nocturna **sí cuenta** contra el tope de 2 horas); el motor debe registrar y marcar exceso.

**Ejemplo 2. Jornada con llegada tarde injustificada.** Misma jornada del ejemplo 1. Miércoles, entrada real 8:45 a.m., salida real 5:00 p.m., con 1 hora de almuerzo. Clasificación: 8:00 a.m.–8:45 a.m. (0,75 h) = **no trabajado**, descontable; 8:45 a.m.–12:00 p.m. (3,25 h) y 1:00 p.m.–5:00 p.m. (4 h) = **7,25 horas ordinarias diurnas** (categoría 1). Total reconocido: 7,25 h. No hay extras.

**Ejemplo 3. Turno nocturno completo en día hábil.** Jornada programada lunes a viernes 9:00 p.m.–5:00 a.m. (del día siguiente), 30 minutos de descanso 1:00 a.m.–1:30 a.m. Entrada real 9:00 p.m. lunes, salida real 5:00 a.m. martes, ambas puntuales. Clasificación: 9:00 p.m.–1:00 a.m. (4 h) + 1:30 a.m.–5:00 a.m. (3,5 h) = **7,5 horas ordinarias nocturnas** (categoría 2, recargo 35%). Total: 7,5 h.

**Ejemplo 4. Turno que cruza la frontera de las 7:00 p.m.** Jornada 2:00 p.m.–10:00 p.m. sábado hábil, con 30 minutos de cena 6:00 p.m.–6:30 p.m. Entrada y salida puntuales. Clasificación: 2:00 p.m.–6:00 p.m. (4 h) + 6:30 p.m.–7:00 p.m. (0,5 h) = **4,5 horas ordinarias diurnas** (categoría 1); 7:00 p.m.–10:00 p.m. (3 h) = **3 horas ordinarias nocturnas** (categoría 2). Total: 7,5 h. Este ejemplo ilustra el impacto del nuevo horario de la Ley 2466: antes del 25 de diciembre de 2025, las tres horas finales habrían sido diurnas (solo desde las 9:00 p.m. comenzaba la noche); ahora generan recargo del 35%.

**Ejemplo 5. Jornada nocturna a caballo entre sábado hábil y domingo.** Jornada 10:00 p.m. sábado – 6:00 a.m. domingo, sin descansos intra-jornada (turno corto). Entrada y salida puntuales. Clasificación: 10:00 p.m.–12:00 a.m. (2 h) = **2 horas ordinarias nocturnas** en día hábil (categoría 2, recargo 35%); 12:00 a.m.–6:00 a.m. (6 h) = **6 horas ordinarias nocturnas dominicales** (categoría 6, recargo aditivo 80% + 35% = 115%). Total: 8 h, divididas en dos categorías por el cruce calendario. Este es el caso canónico que exige el criterio calendario.

**Ejemplo 6. Festivo "Emiliani" que cae en domingo.** El 1 de noviembre de un año dado cae en domingo. Como es festivo trasladable, el **lunes 2 de noviembre** es el festivo con descanso remunerado. Un trabajador con jornada 8:00 a.m.–5:00 p.m. de lunes a viernes que labora ese **lunes 2** (festivo trasladado), entrada y salida puntuales, clasifica: 8 horas **dominicales/festivas ordinarias diurnas** (categoría 5, recargo 80%). El domingo 1 anterior, si trabajó, se clasifica solo como dominical (categoría 5 o 6), no se acumula el recargo del festivo fijo, porque el festivo se trasladó al lunes.

**Ejemplo 7. Jornada flexible art. 161 literal a.** Contrato pacta jornada flexible: lunes 9 h, martes 9 h, miércoles 9 h, jueves 9 h, viernes 8 h = 44 h semanales. Entrada y salida puntuales todos los días, con 1 h de almuerzo cada día. Clasificación: **todas las horas son ordinarias diurnas o nocturnas sin recargo por suplementario**, aunque algunos días excedan 8 horas, porque la ley exceptúa expresamente el recargo suplementario cuando se cumple el régimen de 4–9 h diarias y promedio semanal dentro del tope. Si un día se laboraran 10 h (por fuera del tope 9 h del literal a), la décima hora sí sería extra de la categoría correspondiente.

**Ejemplo 8. Trabajo habitual en domingo con extras nocturnas.** Vigilante privado (fuera del régimen del art. 161 lit. d). Jornada programada domingo 12:00 p.m.–8:00 p.m., trabajo habitual (4 domingos al mes). Entrada real 12:00 p.m., salida real 10:00 p.m., con autorización expresa. Descanso 4:00 p.m.–4:30 p.m. Clasificación: 12:00 p.m.–4:00 p.m. + 4:30 p.m.–7:00 p.m. (6,5 h) = **6,5 horas dominicales ordinarias diurnas** (categoría 5, 80%); 7:00 p.m.–8:00 p.m. (1 h) = **1 hora dominical ordinaria nocturna** (categoría 6, 80% + 35%); 8:00 p.m.–10:00 p.m. (2 h) = **2 horas extras nocturnas dominicales** (categoría 8, 75% + 80%). Total: 9,5 h. Como es habitual, además del pago con recargos tiene derecho a **descanso compensatorio remunerado** dentro de la semana siguiente (art. 181 CST); ese descanso compensatorio no altera la clasificación de las horas trabajadas, solo impone una obligación adicional en la agenda.

---

## 9. Calendario de la reforma: qué cambia después de abril de 2026

El motor debe ser **parametrizable en el tiempo** porque varios hitos posteriores a abril de 2026 modifican las categorías y los topes sin requerir reforma adicional. El 1 de julio de 2026 el recargo dominical sube de 80% a 90%; el motor debe actualizar la etiqueta de las categorías 5, 6, 7 y 8 (sin cambiar la clasificación temporal, solo el porcentaje asociado). El 15 de julio de 2026 la jornada semanal máxima baja de 44 a 42 horas, lo cual altera la base mensual (de 220 a 210 horas) y el tope combinado semanal (de 56 a 54 horas); esto implica recalcular el umbral a partir del cual horas dentro de la "jornada programada" pueden ser reclasificadas como extras si el empleador no ajusta el horario programado. El 1 de julio de 2027 el recargo dominical llega al 100% pleno y desaparece el régimen transitorio del parágrafo del art. 179 CST.

---

## 10. Conclusiones y zonas que requieren validación legal

La clasificación correcta de horas en el Colombia de abril de 2026 depende de una **matriz de tres ejes ortogonales**: día calendario (hábil/domingo/festivo), franja horaria (diurna/nocturna con corte a las 7:00 p.m.), y posición respecto de la jornada programada (dentro/fuera). Las ocho categorías resultantes son exhaustivas y mutuamente excluyentes por minuto trabajado. El motor debe operar en precisión de minuto, segmentar por fronteras, aplicar el criterio calendario estricto para domingos y festivos, sumar aditivamente los recargos cuando se combinan ejes independientes, y **nunca eliminar horas por exceder topes legales**: debe clasificarlas y alertar, porque la doctrina de irrenunciabilidad exige pagarlas incluso cuando el tope se violó.

Cuatro zonas ameritan **validación explícita con abogado laboralista** antes de lanzar a producción: (i) el tratamiento de horas trabajadas por encima del tope del art. 167A CST (deben pagarse, pero la forma de documentar la sanción al empleador no tiene guía jurisprudencial única); (ii) la política contractual del día de descanso distinto al domingo bajo el parágrafo 3 del art. 179 (bajo controversia académica, sin fallo de la Corte Constitucional); (iii) la determinación de "conocimiento y tolerancia" del empleador para calificar horas extras tácitas, que es probatoria y caso a caso; y (iv) el redondeo de fracciones, dado que no hay norma legal y cualquier política debe respetar mínimos.

La recomendación técnica final es construir el motor como un **pipeline de segmentación y clasificación desacoplado del pipeline de liquidación monetaria**, de modo que los cambios de porcentaje del calendario 2026-2027 afecten únicamente a la capa de cálculo. Las categorías legales son estables; los valores no.