# CA-ADR-0030: Fallos sincronos para comandos HTTP sin consumidores downstream

## Estado

Aceptado.

## Contexto

El issue #349 (terminar la vinculacion de un colaborador) es el segundo comando del ciclo de vida
de `ColaboradorAggregateRoot` (desglose #348-#357) y el primero de esa cadena en violar una regla
de negocio evaluada por el propio aggregate, no una precondicion de orquestacion del handler. El
borrador inicial del issue proponia `TerminacionVinculacionFallida`, un evento de fallo persistido,
por lectura literal de MEF-ADR-0004 capa 3 ("eventos de fallo para reglas de negocio del
aggregate"). Al refinar el issue (sesion planner, 2026-08-11) esa lectura resulto incompleta: la
razon de ser de un evento de fallo en MEF-ADR-0004 es que **un consumidor downstream reacciona** a
el (un handler de Service Bus, una saga). `TerminarVinculacion` no tiene ninguno: el dominio
Colaboradores es event-sourcing puro en este comando, "Consumidores: ninguno", y el trigger es
HTTP -- un canal sincrono que ya tiene un mecanismo de respuesta inmediata (el status code).

Persistir `TerminacionVinculacionFallida` sin un lector tenia tres costos concretos:

1. **Deja ciego al caller.** Sin un evento de exito posterior que lo desmienta, el caller recibe
   202 Accepted por un comando que en realidad fue rechazado -- el patron correcto para eso es
   sincronizar con el resultado ya, no diferirlo a un evento que nadie procesa.
2. **Contamina el stream del colaborador legitimo** con hechos que nunca le pasaron: el stream de
   event sourcing es la fuente de verdad de lo que ocurrio, y un intento rechazado no es un hecho
   del dominio, es un hecho de la interaccion HTTP.
3. **Estrena un patron sin lector.** Ningun handler de Colaboradores suscribe eventos de fallo
   propios; anadir el primero sin que nada lo consuma es deuda tecnica de dia uno.

Este ADR fija la decision para que los comandos hermanos de la misma cadena (#350 reingreso, #351,
#352 ajuste de fechas, #355) la apliquen sin volver a discutirla, y para que `reviewer` no la marque
como desviacion cuando la vea repetida.

## Decision

**Un comando HTTP sin consumidores downstream que reciba de un aggregate el rechazo de una regla de
negocio responde ese rechazo con un status code sincrono (409 Conflict via
`InvalidOperationException`, o 404 Not Found via `KeyNotFoundException` cuando la precondicion es
"el recurso no existe"), nunca con un evento de fallo persistido.**

Los eventos de fallo persistidos (MEF-ADR-0004 capa 3) se reservan para flujos donde existe un
consumidor que reacciona a la falla -- un handler de Service Bus, una saga, un proceso downstream
que necesita saber que algo no ocurrio para compensar o notificar. Si el canal es HTTP y no hay
consumidor, el status code sincrono ya resuelve el mismo problema sin pagar el costo de persistir
un hecho que nadie lee.

### Mecanismo: el aggregate declina con resultado, nunca lanza

El aggregate sigue sin lanzar excepciones para logica de dominio (MEF-ADR-0004 capa 4) y sin
interrogar su propio estado desde fuera (Tell-don't-Ask, MEF-ADR-0012). En vez de eso, el metodo que
ejerce la regla de negocio **responde el resultado de la operacion** -- exito o la razon puntual del
rechazo -- y dos causas ya no requieren un evento para comunicarse:

- El aggregate no muta nada y no agrega eventos a `_uncommittedEvents` cuando declina.
- El handler consulta unicamente ese resultado (nunca el estado interno) y traduce la razon de
  rechazo a la excepcion correspondiente, con mensaje `.resx` (MEF-ADR-0009).

Precedente interno de "declinar sin emitir": `ControlDiarioAggregateRoot.AdicionarMarcacion`
(idempotencia de marcaciones duplicadas) ya ignora silenciosamente sin lanzar ni emitir. Este ADR
generaliza ese mecanismo para el caso en que el handler **si** necesita distinguir la razon del
rechazo (aqui, "ya terminada" vs "fecha anterior al inicio"), usando el valor de retorno del metodo
en vez de un booleano o un efecto silencioso.

### Cuando SI corresponde un evento de fallo persistido

Este ADR no reemplaza MEF-ADR-0004 capa 3 en general -- solo fija el criterio de decision para el
caso "HTTP + sin consumidor". Un evento de fallo sigue siendo la eleccion correcta cuando:

- El trigger no es HTTP (Service Bus, timer) y por tanto no hay un canal de respuesta sincrono.
- Existe un consumidor real (otro dominio, una saga, una proyeccion) que necesita reaccionar al
  rechazo -- no solo un futuro hipotetico.

## Alternativas consideradas

**Evento de fallo `TerminacionVinculacionFallida` + 202 Accepted** (propuesta original del
borrador). Descartada: caller ciego hasta que consultara el stream (que no expone HTTP en este
issue), stream contaminado con hechos que no ocurrieron, patron sin consumidor. Ver "Contexto".

**Validar la regla en el handler, antes de invocar al aggregate.** Descartada: moveria la logica de
negocio ("una vinculacion no puede terminarse dos veces", "la fecha efectiva no puede ser anterior
al inicio") fuera del aggregate, que es quien tiene el estado necesario para evaluarla. El handler
quedaria interrogando el estado interno del aggregate para decidir -- exactamente lo que
Tell-don't-Ask (MEF-ADR-0012) prohibe.

**Un booleano (`bool TryTerminarVinculacion(...)`) en vez de un resultado con razon.** Descartada
para este caso especifico: el handler necesita distinguir "ya terminada" (409 con un mensaje) de
"fecha anterior al inicio" (409 con otro mensaje), y un booleano no transporta esa distincion sin
que el handler vuelva a interrogar el estado del aggregate para averiguar cual fue.

## Consecuencias

### Positivas

- El caller HTTP recibe la respuesta correcta (409/404/202) en la misma request, sin depender de
  que algo lea un evento que nadie procesa.
- El stream de `ColaboradorAggregateRoot` solo contiene hechos que efectivamente ocurrieron.
- El patron es replicable sin discusion en #350/#351/#352/#355: cualquier comando HTTP de esa
  cadena que necesite rechazar una regla de negocio del aggregate usa el mismo mecanismo
  (declinar con resultado -> handler traduce -> status code).
- Simetria con el comando hermano de creacion (#330): crear dos veces -> 409; terminar dos veces
  -> 409. Misma familia de respuesta para la misma familia de violacion ("el hecho ya ocurrio").

### Negativas y deuda asumida

- Si un futuro consumidor downstream (por ejemplo, una integracion con nomina que reaccione a
  intentos de terminacion rechazados) apareciera para este comando, este ADR no lo cubre: ese caso
  requeriria revisar la decision y anadir el evento de fallo que hoy se descarta. Costo aceptado
  deliberadamente (ver "Notas tecnicas" del issue #349: la asimetria de reversa es barata --
  MEF-ADR-0005 -- agregar el evento despues es aditivo).
- El enum de resultado (`ResultadoTerminacionVinculacion`) es especifico de este comando; comandos
  hermanos con distintas razones de rechazo definiran su propio tipo de resultado. Este ADR no
  fija una forma generica de "resultado de operacion" para todo el dominio, solo el mecanismo
  (declinar sin lanzar, sin emitir; el handler traduce en el borde).

## Referencias

- MEF-ADR-0004 (manejo de errores en event sourcing): capa 2 (excepciones de orquestacion del
  handler -> HTTP status codes), capa 3 (eventos de fallo con consumidor), capa 4 (el aggregate
  nunca lanza). Este ADR precisa el criterio de eleccion entre capa 2 y capa 3 para comandos HTTP.
- MEF-ADR-0012 (Tell-don't-Ask, estilo de modelado de objetos de dominio): el aggregate decide y
  responde el resultado; el handler no interroga su estado interno.
- MEF-ADR-0009 (mensajes en `.resx` por aggregate/handler): los mensajes de las excepciones
  traducidas viven en el `.resx` del handler, no del aggregate (el aggregate no lanza).
- Precedente de "declinar sin emitir": `ControlDiarioAggregateRoot.AdicionarMarcacion`
  (idempotencia de marcaciones duplicadas, ControlHoras).
- Precedente de endpoint 404+409: `SolicitarProgramacionTurnoFunction.FunctionEndpoint`
  (Programacion).

## Control de cambios

- 2026-08-11: creacion (issue #349). Fija el patron "declinar con resultado + status code sincrono"
  para comandos HTTP sin consumidores downstream, descartando el evento de fallo persistido que el
  borrador original proponia.
