---
fecha: 2026-04-26
hora: 21:50
sesion: bug-investigator
tema: smoke test del PR 157 falla al deserializar DiaCalculado por ausencia de resolver ADR-0015 en ServiceBusFixture
---

## Sintoma reportado

Run https://github.com/augusto-romero-arango/Bitakora.ControlAsistencia/actions/runs/24973524943/job/73122438574 (post-merge del PR #157, commit 0730c0b en main) muestra 1 test fallido de 10 en el smoke-test del dominio ControlHoras:

- Test: `RegistrarMarcacionSmokeTests.DebePublicarDiaCalculadoYPersistirMarcacionAdicionada_CuandoMarcacionGeneraNuevoEvento`.
- Excepcion: `System.NotSupportedException : Deserialization of types without a parameterless constructor, a singular parameterized constructor, or a parameterized constructor annotated with 'JsonConstructorAttribute' is not supported. Type 'Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects.DetalleRetardo'. Path: $.desgloseHoras.retardoTotal | LineNumber: 0 | BytePositionInLine: 472`.
- Punto de fallo: `tests/Bitakora.ControlAsistencia.ControlHoras.SmokeTests/Fixtures/ServiceBusFixture.cs:91` (linea del `JsonSerializer.Deserialize<T>` dentro de `WaitForMessageAsync<T>`).

## Investigacion

1. `gh run view --job 73122438574 --log-failed`: el test persiste `marcacion_adicionada` correctamente (HTTP 202 + assert de Postgres pasa). La falla aparece despues, al recibir `DiaCalculado` desde el topic `dia-calculado`.
2. App Insights ultimas 12h:
   - `./scripts/appinsights-query.sh exceptions`: 0 excepciones.
   - `./scripts/appinsights-query.sh function-errors`: 0 funciones con requests fallidas.
   - `./scripts/appinsights-query.sh dead-letters`: 0 traces de dead-letter.
   - El servidor publica `DiaCalculado` correctamente; el problema es exclusivo del cliente.
3. Codigo correlacionado:
   - `src/Bitakora.ControlAsistencia.Contracts/ControlHoras/ValueObjects/DetalleRetardo.cs:21-33`: ctor parametrizado privado + ctor vacio privado. Cumple ADR-0015 patron sealed class + factory.
   - `src/Bitakora.ControlAsistencia.Contracts/ControlHoras/Eventos/DiaCalculado.cs`: contiene `DesgloseHoras DesgloseHoras` (record con ctor publico) que a su vez contiene `DetalleRetardo RetardoTotal`. El comentario in-source dice "Sin ConfigurarSerializacion propio: STJ deserializa via el ctor publico" -- aplica al evento contenedor pero no a los value objects internos.
   - `src/Bitakora.ControlAsistencia.ControlHoras/Infraestructura/ConfiguracionSerializacionControlHoras.cs:30`: del lado servidor se registra `DetalleRetardo.ConfigurarSerializacion(resolver)`. Por eso Marten persiste y Wolverine publica sin error.
   - `tests/Bitakora.ControlAsistencia.ControlHoras.SmokeTests/Fixtures/ServiceBusFixture.cs:72`: el fixture construye `JsonSerializerOptions` solo con `PropertyNameCaseInsensitive = true`. Sin `TypeInfoResolver`. Sin `ConfigurarSerializacion`.
   - El fixture de Programacion (`tests/Bitakora.ControlAsistencia.Programacion.SmokeTests/Fixtures/ServiceBusFixture.cs:56`) tiene el mismo bug latente: usa el mismo patron, pero hasta hoy solo deserializaba `ProgramacionTurnoDiarioSolicitada`, cuyo grafo no contiene value objects ADR-0015 sealed.
4. Wolverine 5.18 publica con STJ + camelCase usando el `JsonSerializerOptions` registrado en el host (que en `Program.cs:39-50` se configura via `ConfigureMarten`, lo que tambien afecta al sender porque comparten options en el setup actual). El JSON wire incluye los 4 campos privados (`tiempoRetardado`, `tiempoCompensado`, `minutosRetardados`, `minutosCompensados`) -- el `BytePositionInLine: 472` lo confirma indirectamente.

## Diagnostico

H1 (confianza muy alta): el smoke test deserializa el mensaje del Service Bus con un `JsonSerializerOptions` plano que no incluye los modifiers de ADR-0015. STJ no puede instanciar `DetalleRetardo` porque la clase no expone un ctor parameterless publico, ni un parameterized unico publico, ni `[JsonConstructor]`. La excepcion del binder de STJ es exactamente la documentada por Microsoft para este escenario.

El handler servidor esta sano (Postgres y publicacion confirmadas). Es un bug de tooling de pruebas: el `ServiceBusFixture` necesita usar el mismo resolver que el dominio.

## Acciones

Pendiente de validacion del usuario:

- Crear issue `bug,tipo:refactor,dom:control-horas,estado:listo`:
  - Titulo: "Aplicar resolver de serializacion ADR-0015 al ServiceBusFixture de smoke tests".
  - Fix: en `ServiceBusFixture.WaitForMessageAsync<T>` construir `JsonSerializerOptions` con `TypeInfoResolver = new DefaultJsonTypeInfoResolver()` y aplicar los `ConfigurarSerializacion` correspondientes (ej. via un helper compartido tipo `ConfiguracionSerializacionControlHoras.ConfigurarResolver` exportado desde `Infraestructura` o un equivalente nuevo en `Contracts`).
  - Alcance: extender preventivamente al fixture de `Programacion.SmokeTests` para evitar el mismo bug cuando Programacion empiece a consumir eventos con value objects ADR-0015 (decision pendiente).
- Sin workaround productivo: el deploy esta sano, solo afecta la signal del workflow de smoke tests sobre main.

## Preguntas abiertas

1. Conviene exponer un helper de serializacion compartido en `Contracts` para que ambos fixtures (y el dominio servidor) registren los mismos modifiers, o duplicar el setup en cada fixture acepta el costo segun ADR-0024?
2. Hay otros tests de smoke planeados que deserialicen eventos con value objects ADR-0015 (ej. cuando #116 calcule retardo real) que justifiquen ampliar el alcance del fix ahora.
3. Verificar si Wolverine respeta el `TypeInfoResolver` del `JsonSerializerOptions` registrado o si usa su propia configuracion separada (no es bloqueante para este bug porque la publicacion no fallo, pero util documentarlo).
