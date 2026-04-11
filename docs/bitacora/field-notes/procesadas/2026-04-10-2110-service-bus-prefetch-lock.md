---
fecha: 2026-04-10
hora: 21:10
sesion: bug-investigator
tema: MessageLockLost por prefetchCount alto con maxConcurrentCalls 1
---

## Contexto

Alerta de Azure reporto multiples errores en 5 minutos. La funcion `AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada` (ControlHoras) fallo con `ServiceBusException: MessageLockLost` al intentar `DeadLetterMessageAsync`.

Hipotesis inicial del usuario: la politica de TTL de 5 minutos en la subscription `smoke-tests` podria estar causando la expiracion. Se descarto porque cada subscription mantiene su propia copia del mensaje — el TTL de una no afecta a otra.

## Diagnostico

### Causa raiz: prefetchCount 10 + maxConcurrentCalls 1 + cold start

Configuracion en `host.json` de ControlHoras:

```json
{
  "prefetchCount": 10,
  "maxConcurrentCalls": 1,
  "autoCompleteMessages": false,
  "maxAutoLockRenewalDuration": "00:05:00"
}
```

Lock duration de la subscription: **1 minuto** (default de Azure, no configurado en Terraform).

Flujo del incidente:

1. Smoke tests publicaron eventos `ProgramacionTurnoDiarioSolicitada` al topic
2. La Function App arranco en cold start (~79 segundos)
3. `prefetchCount: 10` trajo 10 mensajes de golpe, cada uno con lock de 1 minuto
4. Mientras procesaba el primer mensaje (79s por cold start), el lock de los otros 9 expiro
5. Service Bus devolvio esos 9 mensajes a la subscription (incrementando DeliveryCount)
6. Cuando la funcion intento `CompleteMessageAsync` en el mensaje #2, el lock ya no existia → `MessageLockLost`
7. El catch block intento `DeadLetterMessageAsync` → tambien fallo por `MessageLockLost` (no se puede operar un mensaje sin lock)
8. La excepcion escalo al runtime y genero la alerta

Los mensajes no se perdieron: Service Bus los re-entrego automaticamente y se procesaron exitosamente en reintentos posteriores (Function App ya caliente, ~282ms por mensaje).

### Defecto secundario en FunctionEndpoint.cs

El catch block no distingue errores de infraestructura (lock lost) de errores de negocio:

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "...");
    await messageActions.DeadLetterMessageAsync(message);  // falla si el lock expiro
}
```

Cuando el lock se perdio, no tiene sentido intentar dead-letter — va a fallar siempre. Esto duplica las excepciones (la original + la del dead-letter fallido).

## Leccion de arquitectura: prefetchCount con procesamiento secuencial

**Regla**: `prefetchCount` alto solo tiene sentido cuando `maxConcurrentCalls` es alto. Con procesamiento secuencial (`maxConcurrentCalls: 1`), prefetch trae N mensajes con lock corriendo pero solo procesa 1 a la vez. Los N-1 restantes tienen el reloj corriendo sin ser atendidos.

**Analisis de impacto de mover prefetchCount a 0**:

- Con procesamiento de ~280ms por mensaje y latencia de red de ~3ms a Service Bus, el overhead es ~300ms por cada 100 mensajes (despreciable).
- El cuello de botella es la escritura a PostgreSQL via Marten, no la red a Service Bus.
- Prefetch alto tiene sentido para miles de mensajes/segundo con procesamiento sub-10ms. No es nuestro caso.

**Guia de Microsoft**: `prefetchCount = maxConcurrentCalls x 20`. Con `maxConcurrentCalls: 1`, el valor correcto es 0 o 1.

## Acciones identificadas

1. **Reducir `prefetchCount` a 0** en `host.json` de ControlHoras (y revisar otros dominios)
2. **Mejorar catch en FunctionEndpoint** para detectar `MessageLockLost` y solo loggear sin intentar dead-letter
3. **Considerar configurar `lock_duration`** en Terraform si en el futuro se sube el prefetch (hoy no es necesario con prefetch 0)

## Datos de soporte

- Subscription: `control-horas-escucha-programacion`
- Lock duration: PT1M (default, no configurado en Terraform)
- Mensajes activos post-incidente: 0
- Dead letter post-incidente: 0
- Primera invocacion: 79,210ms (cold start)
- Invocaciones exitosas posteriores: ~282ms
