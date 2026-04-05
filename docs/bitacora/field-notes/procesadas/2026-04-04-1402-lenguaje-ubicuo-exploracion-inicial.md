---
fecha: 2026-04-04
hora: 14:02
sesion: event-stormer
tema: Exploración inicial del lenguaje ubicuo del dominio
---

## Contexto
Primera sesión de knowledge crunching para definir el lenguaje ubicuo del sistema de Control de Asistencia. El proyecto tiene decisiones arquitectónicas tomadas (Event Sourcing, Function Apps por dominio) pero el vocabulario del dominio no estaba formalizado. Se partió desde cero, sin asumir términos previos.

## Descubrimientos

### Vocabulario definido

| Término | Definición |
|---|---|
| **Marcación** | Registro crudo de entrada/salida proveniente de un sistema externo. Contiene: día/hora, identificador de persona, opcionalmente tipo (entrada/salida), y un lugar/dispositivo donde sucedió |
| **Turno** | Bloque de trabajo definido, compuesto por una o más franjas. Concepto operativo (a diferencia de "jornada" que es jurídico) |
| **Franja** | Segmento continuo de tiempo dentro de un turno. Puede ser de trabajo o de descanso. Resuelve elegantemente dos modelos: el estricto (solo franjas de trabajo, los descansos son los huecos) y el flexible (franjas de trabajo + franjas de descanso declaradas que se descuentan sin medir con marcaciones) |
| **Programación de turnos** | Asignación de turnos a empleados para períodos específicos |
| **Depuración** | Proceso automático que cruza marcaciones con el turno asignado y produce intervalos de presencia. Decide qué marcaciones son relevantes y cuáles se descartan, dejando rastro de su razonamiento |
| **Intervalo de presencia** | Pareja inicio-fin que representa un período continuo de presencia del empleado. No es lo mismo que "tiempo trabajado" porque puede contener descansos dentro |
| **Anomalía** | Caso sospechoso o incompleto detectado por el sistema. Se eligió este término por su peso estadístico, pensando en escalabilidad futura hacia detección por IA |
| **Conciliación** | Proceso manual donde un humano resuelve anomalías o corrige resultados de la depuración. A diferencia de la depuración (automática), la conciliación es humana |
| **Discriminación de horas** | Desglose del tiempo trabajado por concepto. Se prefirió sobre "liquidación de horas" porque liquidación suena más a pago |
| **Concepto** | Categoría de clasificación del tiempo (hora ordinaria diurna, recargo nocturno, hora extra diurna, etc.). El sistema discrimina tiempo, no dinero -- los porcentajes y valores son responsabilidad del sistema de nómina |
| **Retardo** | Llegada tarde respecto al turno asignado |

### Distinción clave: Jornada vs Turno
- **Jornada** es el concepto jurídico -- habla de duraciones y límites legales (jornada máxima, jornada ordinaria)
- **Turno** es el concepto operativo -- habla de la ejecución real del horario (de 7AM a 5PM)

### Propuesta de valor del sistema
El sistema debe reducir la necesidad de operaciones manuales. El éxito se mide en que los aprobadores confíen en él y solo se concentren en los pocos casos excepcionales, no en revisar uno a uno cada empleado cada día.

### El sistema discrimina tiempo, no dinero
El sistema reporta "el empleado trabajó 2 horas en concepto de hora extra nocturna". El porcentaje de recargo y el valor monetario son responsabilidad del sistema de nómina.

### Nombre del proyecto
"Control de Asistencia" se mantiene, pero ahora con definición consciente:
- **Control** abarca la captura/depuración de marcaciones Y el control sobre la discriminación del tiempo laborado
- **Asistencia** se refiere a la presencia del empleado en su lugar de trabajo

### Usuarios y sistemas

**Usuarios humanos directos:**

1. **Programador de turnos** -- persona responsable de garantizar la cobertura operativa de uno o más sitios de trabajo. Necesita definir turnos con franjas, asignarlos a empleados, garantizar cobertura y cumplir límites legales de jornada laboral máxima y trabajo suplementario.

2. **Aprobador** -- revisa resultados de depuración, atiende anomalías, da visto bueno. Puede ser la misma persona que el programador de turnos. Su frustración principal: tener que verificar todo manualmente por no poder confiar en el sistema.

**Sistemas externos (también son usuarios):**
- **Sistema de marcación** -- nos envía las marcaciones crudas
- **Sistema de nómina** -- recibe la discriminación de horas aprobada
- **Sistema de empleados / RRHH** -- nos informa contrataciones, terminaciones de contrato, datos del empleado

**Consumidores indirectos (no son usuarios del sistema):**
- **Liquidador de nómina** -- consume el resultado a través del sistema de nómina
- **Empleado** -- consulta su información a través de otros sistemas (ej: intranet). Será usuario directo cuando se construya el módulo de registro de marcaciones

### Dos modelos de turno descubiertos
- **Modelo estricto**: la empresa define múltiples franjas de trabajo. Los descansos son los huecos entre franjas. Cada franja necesita marcaciones.
- **Modelo flexible**: una sola franja grande con descansos declarados que se descuentan sin medir. Solo importa la marcación de entrada y salida del día.

### La depuración necesita el turno como input
No se puede depurar marcaciones sin conocer el turno asignado. El turno es el marco de referencia para interpretar qué marcaciones son relevantes y cómo agruparlas (ejemplo del turno partido: sin saber los bloques, el sistema no puede distinguir dos intervalos separados).

## Decisiones

- Los términos listados arriba son el lenguaje ubicuo inicial del proyecto
- Se descartó "fichaje" y vocabulario de España por la distancia léxica con Colombia
- Se descartó "jornada" como término operativo (se reserva para el concepto jurídico)
- Se descartó "liquidación de horas" a favor de "discriminación de horas"
- Se descartó "tiempo efectivo" porque el tiempo es la unidad de respuesta, no el rango
- Se descartó "asistencia depurada" porque el salto de marcación a asistencia no es natural
- Los roles de programador de turnos y aprobador son roles del sistema, no cargos -- pueden recaer en la misma persona
- -> candidato a ADR: Lenguaje ubicuo del dominio (glosario formal)

## Descartado

- **Fichaje**: término de España, no aplica al contexto colombiano
- **Jornada** como término operativo: se reserva para el ámbito jurídico
- **Liquidación de horas**: suena a pago, no a clasificación
- **Tiempo efectivo**: describe la unidad de resultado, no el rango
- **Asistencia depurada**: el salto semántico de marcación a asistencia no es natural
- **Tramo, bloque, segmento** para las partes del turno: franja ganó por ser término ya existente en programación de horarios
- **Planificador, coordinador, supervisor, asignador** para el rol de programación: programador de turnos es más preciso para lo que hace en el sistema

## Preguntas abiertas

1. **El lugar de trabajo** -- punto de venta, sede, fábrica, obra. ¿Cómo lo llamamos en el sistema? Necesita homologación como se hizo con programador de turnos
2. **El empleado como concepto** -- ¿le decimos empleado, trabajador, colaborador? ¿Qué datos nos importan de él?
3. **Los dominios del sistema** -- el ADR-0001 menciona Marcaciones, Empleados, Liquidación y Notificaciones. Con el vocabulario descubierto hoy, ¿siguen siendo los correctos? ¿Dónde viven los turnos, la depuración, la discriminación?
4. **El dominio "Programación"** ya existe en el código -- ¿es ahí donde viven los turnos y las franjas?
5. **El período temporal** -- ¿cómo se delimita la discriminación de horas? ¿Semanal, quincenal, mensual? ¿Lo define cada empresa?
6. **Novedades** -- permisos, incapacidades, ausencias justificadas. Pendiente para otra sesión
7. **Conceptos completos** -- la lista de conceptos de discriminación se irá complementando conforme avance la implementación

## Referencias

- ADRs consultados: 0001 (Function App por dominio), 0005 (naming de eventos), 0006 (Event Sourcing con Marten)
- No se crearon issues en esta sesión
- Candidato a ADR: glosario formal del lenguaje ubicuo
