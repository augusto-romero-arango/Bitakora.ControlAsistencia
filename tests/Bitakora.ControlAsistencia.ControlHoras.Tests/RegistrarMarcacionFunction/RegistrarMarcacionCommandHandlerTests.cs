// HU-105 / issue #270: Registrar marcacion de entrada o salida
// CA-4: el handler publica RegistroDeMarcacionCreado (contrato de bus, PrivateEvents.ControlHoras)
// empaquetado por el traductor del aggregate, tras StartStream. MarcacionRegistrada (evento de
// dominio persistido en el stream) ya NO cruza el bus -- deja de implementar IPrivateEvent (CA-3).

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

    protected override ICommandHandlerAsync<RegistrarMarcacion> Handler =>
        new RegistrarMarcacionCommandHandler(EventStore, PrivateEventSender);

    private static MarcacionRegistrada CrearMarcacionRegistrada(
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        new(EmpleadoId, TimestampNormalizado, tipoMarcacion, dispositivoId);

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
}
