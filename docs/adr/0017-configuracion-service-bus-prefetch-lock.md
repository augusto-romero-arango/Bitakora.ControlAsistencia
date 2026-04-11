# ADR-0017: Configuracion de prefetchCount y manejo de MessageLockLost en Service Bus

## Estado

Aceptado

## Contexto

Un incidente el 2026-04-10 genero multiples errores `ServiceBusException: MessageLockLost` en la
funcion `AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada` (dominio ControlHoras). La alerta
de pico de excepciones (ADR-0009) detecto 8 fallos en 22 segundos.

La causa raiz fue la combinacion de tres factores:

1. **`prefetchCount: 10`** en host.json: el SDK de Service Bus pre-descarga 10 mensajes de la
   subscription, bloqueandolos (lock) simultaneamente.
2. **`maxConcurrentCalls: 1`**: los mensajes se procesan de a uno. Los 9 restantes esperan en
   buffer con el lock corriendo.
3. **Lock duration: 1 minuto** (default de Azure, no configurado en Terraform): si el primer
   mensaje tarda mas de 60 segundos (cold start de ~79s), los demas pierden el lock antes de
   ser procesados.

Cuando el lock expira, Service Bus devuelve el mensaje a la subscription para re-entrega. Pero
la Function App aun tiene la copia local y al intentar `CompleteMessageAsync` recibe
`MessageLockLost`. El catch block intenta `DeadLetterMessageAsync`, que tambien falla porque
no se puede operar un mensaje sin lock vigente. Esto genera una excepcion no manejada por cada
mensaje afectado.

Los mensajes no se pierden: Service Bus los re-entrega automaticamente (incrementando
DeliveryCount) y se procesan exitosamente en reintentos posteriores.

## Decision

### 1. prefetchCount a 0 para procesamiento secuencial

Se establece `prefetchCount: 0` en el host.json de todos los dominios que usen
`maxConcurrentCalls: 1`.

Con procesamiento secuencial, prefetch no aporta beneficio significativo: el overhead de red
(~3ms por mensaje) es despreciable frente al tiempo de procesamiento (~280ms de escritura a
PostgreSQL via Marten). La diferencia medida es ~300ms por cada 100 mensajes.

Prefetch alto tiene sentido cuando:
- `maxConcurrentCalls` es alto (muchos mensajes en paralelo)
- El procesamiento por mensaje es muy rapido (< 10ms)
- Se procesan miles de mensajes por segundo

Ninguna de esas condiciones aplica hoy. Si en el futuro el volumen crece, el camino correcto
es subir `maxConcurrentCalls` y ajustar `prefetchCount` proporcionalmente. La guia de Microsoft
es `prefetchCount = maxConcurrentCalls x 20`.

### 2. Manejo explicito de MessageLockLost en FunctionEndpoint

El catch block de los FunctionEndpoint debe detectar `ServiceBusException` con
`Reason == MessageLockLost` y solo loggear una advertencia, sin intentar dead-letter.
Cuando el lock se perdio, Service Bus ya se encargo de devolver el mensaje a la subscription
para re-entrega. Intentar dead-letter es una operacion que siempre va a fallar y genera ruido
innecesario en las alertas.

Para cualquier otra excepcion, el comportamiento actual (dead-letter) se mantiene.

### 3. No se configura lock_duration en Terraform por ahora

Con `prefetchCount: 0`, el lock duration default de 1 minuto es suficiente porque cada mensaje
se lockea justo cuando va a ser procesado. Si `maxAutoLockRenewalDuration: 00:05:00` esta
configurado en host.json, el SDK renueva el lock automaticamente hasta por 5 minutos, cubriendo
procesamiento lento o cold starts.

Si en el futuro se sube prefetchCount, se debera revisar lock_duration proporcionalmente.

## Consecuencias

### Positivas

- Elimina la posibilidad de `MessageLockLost` por mensajes esperando en buffer de prefetch.
- Las alertas de excepciones (ADR-0009) reflejan errores reales, no cascadas de infraestructura.
- Impacto en throughput despreciable (~0.3s por 100 mensajes con procesamiento secuencial).

### Negativas

- Si en el futuro se necesita alto throughput, se debera ajustar `maxConcurrentCalls`,
  `prefetchCount` y potencialmente `lock_duration` en conjunto. Esta decision no es permanente
  sino adecuada al volumen actual.
