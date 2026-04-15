---
fecha: 2026-04-14
hora: 17:05
sesion: event-stormer
tema: Interfaz minima del Registro de Marcacion
---

## Contexto
Se queria definir la interfaz minima que cualquier sistema de marcacion (propio o externo) debe cumplir para enviar datos a nuestro sistema. Se exploraron dos escenarios: dispositivos ajenos del cliente y dispositivos provistos por nosotros, buscando una convergencia en el contrato minimo.

## Descubrimientos

### Dos conceptos, no uno
- **Marcacion**: concepto puro del dispositivo. El acto de capturar el instante de entrada/salida con toda su riqueza (huella, PIN, foto). Le pertenece al sistema de marcacion.
- **Registro de Marcacion**: el dato destilado que nuestro sistema recibe de forma asincrona. Solo contiene los campos minimos. Es lo que nosotros persistimos y procesamos.

### Interfaz minima del Registro de Marcacion

| Campo | Tipo | Obligatorio | Descripcion |
|---|---|---|---|
| EmpleadoId | string | Si | Identificador del empleado segun el sistema de marcacion. Alfanumerico libre. El cliente lo cruza con la identificacion natural en nuestro sistema de Empleados. |
| Timestamp | DateTime | Si | Fecha y hora local de la marcacion segun el dispositivo. Sin offset de zona horaria. |
| TipoMarcacion | enum (entrada/salida) | No | Si el dispositivo lo sabe, lo envia. Si no, se omite del JSON y Depuracion lo infiere. |
| DispositivoId | string | No | Identificador del dispositivo que capturo la marcacion. El cliente le asocia metadata (centro de costos, sede, etc.) en configuracion. |

### Principios de diseno establecidos
- **Conservacion total**: toda marcacion interpretable se guarda. Solo se rechaza lo que no cumple la estructura minima.
- **Laxitud en la recepcion**: la interfaz es lo mas permisiva posible para minimizar perdida de datos.
- **Inteligencia en Depuracion, no en la marcacion**: la marcacion es dato crudo; toda inferencia (direccion, franja, turno) es responsabilidad de Depuracion.
- **Confianza en el dispositivo**: el timestamp lo resuelve el dispositivo, nuestro sistema lo respeta sin modificar. Si el reloj esta desfasado, es problema del cliente.

### EmpleadoId no es nuestro
El EmpleadoId es lo que el sistema de marcacion usa como identificador unico de la persona. Puede ser cedula, codigo interno, lo que sea. Si no hace match con ningun empleado registrado en nuestro sistema, la marcacion se guarda igual como huerfana y el cliente debe resolverlo.

### DispositivoId, no Ubicacion
El campo que identifica de donde vino la marcacion es el ID del dispositivo, no una ubicacion geografica. La geografia (sede, ciudad, porteria) es metadata del dispositivo que vive en configuracion. La tabla de homologacion (DispositivoId -> Centro de costos) permite que multiples dispositivos apunten al mismo centro de costos o no tengan ninguno asignado.

## Decisiones

- **DateTime sin offset**: el sistema opera en hora local del lugar de trabajo. No hay operaciones que comparen timestamps entre zonas horarias. El offset agrega complejidad sin resolver un problema real. -> candidato a ADR
- **Campos opcionales se omiten del JSON**: no se envian como null. null = "quiero borrar", omitir = "no tengo este dato".
- **TipoMarcacion como enum cerrado**: valores "entrada" / "salida" en espanol, validados en la recepcion.
- **Sin ID de deduplicacion**: la deduplicacion se resuelve en Depuracion, no en la recepcion. Agregar deduplicacion en la entrada contradice el principio de conservacion total.
- **Nombre "Registro de Marcacion"**: separa el concepto del dispositivo (Marcacion) del dato que nosotros recibimos y persistimos.
- **DispositivoId en vez de Ubicacion**: lo que recibimos es un identificador de dispositivo, no un concepto geografico. Todo el enriquecimiento geografico vive en configuracion.

## Descartado
- **DateTimeOffset**: se descarto porque el sistema opera en hora local y no necesita aritmetica entre zonas horarias. El caso de DST es un problema del dispositivo, no nuestro.
- **Idempotency Key / ID externo**: se descarto porque todos los escenarios de duplicados se resuelven naturalmente en Depuracion.
- **Ubicacion como nombre de campo**: se descarto porque insinuaba un concepto geografico. Lo que realmente recibimos es un ID de dispositivo.
- **Lugar de trabajo como concepto del Registro de Marcacion**: no pertenece a la marcacion. Si se necesita, se asocia como metadata del dispositivo en configuracion.

## Preguntas abiertas
- Las reglas de inferencia de Depuracion (primera/ultima, por franja, cruce con turno) quedan para una sesion dedicada.
- Recepcion de lotes vs unitario: se diseno el caso unitario. La extrapolacion a lotes queda pendiente.
- Donde vive la configuracion de dispositivos y la tabla de homologacion DispositivoId -> Centro de costos.
- Marcaciones huerfanas (sin match de EmpleadoId): como se presentan al cliente para que las resuelva.

## Referencias
- ADRs consultados: ADR-0002 (la verdad viaja en el evento)
- Fuentes externas consultadas: best practices de APIs (Zalando, Tyk, Appwrite), formatos de timestamp en integraciones con biometricos (ZKTeco, ERPNext), estrategias de idempotencia (Snowplow, Zuplo)
