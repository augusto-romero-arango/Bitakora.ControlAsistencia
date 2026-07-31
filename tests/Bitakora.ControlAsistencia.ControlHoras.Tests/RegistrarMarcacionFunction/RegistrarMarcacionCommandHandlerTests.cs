// HU-105 / issue #270: Registrar marcacion de entrada o salida
// CA-4: el handler publica RegistroDeMarcacionCreado (contrato de bus, PrivateEvents.ControlHoras)
// empaquetado por el traductor del aggregate, tras StartStream. MarcacionRegistrada (evento de
// dominio persistido en el stream) ya NO cruza el bus -- deja de implementar IPrivateEvent (CA-3).
// Issue #275 CA-4: el handler ya no trunca segundos ni invoca "new MarcacionRegistrada(...)" --
// llama al factory MarcacionRegistrada.Crear(...), que trunca y valida (patron TurnoCreado,
// CrearTurnoCommandHandlerTests). El evento esperado en los tests tambien se construye con el
// factory, con el mismo timestamp crudo que recibe el comando -- unica via posible ahora que el
// ctor es privado.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction;

public class RegistrarMarcacionCommandHandlerTests : CommandHandlerAsyncTest<RegistrarMarcacion>
{
    // Datos de prueba fijos
    private const string EmpleadoId = "EMP-001";

    // Timestamp crudo con segundos para verificar la normalizacion al minuto (CA-2)
    private static readonly DateTime Timestamp = new DateTime(2026, 3, 15, 8, 9, 59);

    // CA-2: 08:09:59 truncado al minuto -> 08:09:00
    private static readonly DateTime TimestampNormalizado = new DateTime(2026, 3, 15, 8, 9, 0);

    // CA-5: stream ID determinista {EmpleadoId}:{Timestamp:yyyy-MM-ddTHH:mm:ss}
    private static readonly string StreamId = $"{EmpleadoId}:{Timestamp:yyyy-MM-ddTHH:mm:ss}";

    // Issue #275: el stream que el handler habria abierto si el factory no hubiera rechazado el
    // EmpleadoId vacio -- se afirma vacio para probar que el throw precede a cualquier escritura.
    private static readonly string StreamIdSinEmpleado = $":{Timestamp:yyyy-MM-ddTHH:mm:ss}";

    protected override ICommandHandlerAsync<RegistrarMarcacion> Handler =>
        new RegistrarMarcacionCommandHandler(EventStore, PrivateEventSender);

    // Issue #275: el ctor de MarcacionRegistrada es privado -- la unica via de construccion, tanto
    // para el handler como para el oraculo del test, es Crear(...). Se le pasa el mismo timestamp
    // CRUDO (con segundos) que recibe el comando: si el handler dejara de delegar la normalizacion
    // al factory, este test lo detectaria porque el oraculo tambien pasa por el mismo truncamiento.
    private static MarcacionRegistrada CrearMarcacionRegistrada(
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        MarcacionRegistrada.Crear(EmpleadoId, Timestamp, tipoMarcacion, dispositivoId);

    // Issue #270 CA-4: el contrato de bus que se espera publicado -- construido a mano como oraculo
    // independiente (regla 20), con la misma paridad de campos que MarcacionRegistrada, sin invocar
    // el traductor del aggregate (la logica bajo prueba).
    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        new(EmpleadoId, TimestampNormalizado, tipoMarcacion, dispositivoId);

    // CA-1, CA-2, CA-5: marcacion nueva persiste MarcacionRegistrada en el stream y publica
    // RegistroDeMarcacionCreado (no MarcacionRegistrada) via IPrivateEventSender.
    // CA-2: verifica que 08:09:59 se normaliza a 08:09:00 en ambos tipos.
    [Fact]
    public async Task RegistrarMarcacion_EmiteMarcacionRegistradaYPublicaRegistroDeMarcacionCreado_CuandoMarcacionEsNueva()
    {
        await WhenAsync(new RegistrarMarcacion(EmpleadoId, Timestamp, "ENTRADA", "DEV-001"));

        Then(StreamId, CrearMarcacionRegistrada());
        ThenIsPublishedPrivately(CrearRegistroDeMarcacionCreado());
        And<RegistroDeMarcacionAggregateRoot, string>(StreamId, r => r.EmpleadoId, EmpleadoId);
        And<RegistroDeMarcacionAggregateRoot, DateTime>(
            StreamId, r => r.TimestampNormalizado, TimestampNormalizado);
    }

    // CA-3: TipoMarcacion y DispositivoId son opcionales - null es valido en ambos tipos.
    [Fact]
    public async Task RegistrarMarcacion_PropagaCamposOpcionalesNulos_CuandoElComandoNoLosTrae()
    {
        await WhenAsync(new RegistrarMarcacion(EmpleadoId, Timestamp, null, null));

        Then(StreamId, CrearMarcacionRegistrada(tipoMarcacion: null, dispositivoId: null));
        ThenIsPublishedPrivately(CrearRegistroDeMarcacionCreado(tipoMarcacion: null, dispositivoId: null));
        And<RegistroDeMarcacionAggregateRoot, string?>(StreamId, r => r.TipoMarcacion, null);
        And<RegistroDeMarcacionAggregateRoot, string?>(StreamId, r => r.DispositivoId, null);
    }

    // CA-4, CA-9: duplicado exacto (mismo EmpleadoId + mismo Timestamp crudo = mismo stream ID)
    // Handler retorna silenciosamente: sin nuevos eventos en stream, sin eventos publicados (ninguno
    // de los dos tipos).
    [Fact]
    public async Task RegistrarMarcacion_NoPersisteNiPublica_CuandoStreamYaExiste()
    {
        Given(StreamId, CrearMarcacionRegistrada());

        await WhenAsync(new RegistrarMarcacion(EmpleadoId, Timestamp, "ENTRADA", "DEV-001"));

        Then(StreamId);
        ThenIsPublishedPrivately();
        And<RegistroDeMarcacionAggregateRoot, string>(StreamId, r => r.EmpleadoId, EmpleadoId);
    }

    // Issue #275 CA-3/CA-4: el factory MarcacionRegistrada.Crear valida el EmpleadoId; el throw
    // ocurre en el borde del handler (MEF-ADR-0004), nunca dentro del aggregate. No es un evento de
    // fallo del aggregate -- es un dato invalido que nunca deberia haber llegado hasta aqui.
    // El throw ocurre ANTES de tocar el event store: ni se abre el stream ni se publica el contrato
    // de bus, de modo que un dato invalido no deja rastro parcial.
    [Fact]
    public async Task RegistrarMarcacion_PropagaArgumentExceptionSinPersistirNiPublicar_CuandoEmpleadoIdEsVacio()
    {
        var act = async () => await WhenAsync(
            new RegistrarMarcacion(string.Empty, Timestamp, "ENTRADA", "DEV-001"));

        await act.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.EmpleadoIdVacio}*");
        Then(StreamIdSinEmpleado);
        ThenIsPublishedPrivately();
    }
}
