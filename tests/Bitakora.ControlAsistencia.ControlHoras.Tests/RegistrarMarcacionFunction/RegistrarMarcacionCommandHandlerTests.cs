using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RegistrarMarcacionFunction;

public class RegistrarMarcacionCommandHandlerTests : CommandHandlerAsyncTest<RegistrarMarcacion>
{
    private const string CodigoColaborador = "EMP-001";

    // Timestamp crudo con segundos para verificar la normalizacion al minuto.
    private static readonly DateTime Timestamp = new DateTime(2026, 3, 15, 8, 9, 59);

    private static readonly DateTime TimestampNormalizado = new DateTime(2026, 3, 15, 8, 9, 0);

    // Armado a mano, no via ComputarStreamId: oraculo independiente de la logica bajo prueba.
    private static readonly string StreamId = $"rdm:{CodigoColaborador}:{Timestamp:yyyyMMddTHHmmss}";

    // El stream que el handler habria abierto con CodigoColaborador vacio: se afirma vacio para probar
    // que el throw precede a cualquier escritura.
    private static readonly string StreamIdSinColaborador = $"rdm::{Timestamp:yyyyMMddTHHmmss}";

    protected override ICommandHandlerAsync<RegistrarMarcacion> Handler =>
        new RegistrarMarcacionCommandHandler(EventStore, PrivateEventSender);

    // Recibe el mismo timestamp CRUDO que el comando: si el handler dejara de delegar la normalizacion a
    // Crear(...), el oraculo, que si pasa por ese truncamiento, lo detectaria.
    private static MarcacionRegistrada CrearMarcacionRegistrada(
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        MarcacionRegistrada.Crear(CodigoColaborador, Timestamp, tipoMarcacion, dispositivoId);

    // Oraculo armado a mano, sin invocar el traductor del aggregate (la logica bajo prueba).
    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(
        string? tipoMarcacion = "ENTRADA",
        string? dispositivoId = "DEV-001") =>
        new(CodigoColaborador, TimestampNormalizado, tipoMarcacion, dispositivoId);

    // Publica RegistroDeMarcacionCreado, no MarcacionRegistrada; 08:09:59 se normaliza a 08:09:00 en
    // ambos tipos.
    [Fact]
    public async Task RegistrarMarcacion_EmiteMarcacionRegistradaYPublicaRegistroDeMarcacionCreado_CuandoMarcacionEsNueva()
    {
        await WhenAsync(new RegistrarMarcacion(CodigoColaborador, Timestamp, "ENTRADA", "DEV-001"));

        Then(StreamId, CrearMarcacionRegistrada());
        ThenIsPublishedPrivately(new PublishOptions { GroupId = CodigoColaborador }, CrearRegistroDeMarcacionCreado());
        And<RegistroDeMarcacionAggregateRoot, string>(StreamId, r => r.CodigoColaborador, CodigoColaborador);
        And<RegistroDeMarcacionAggregateRoot, DateTime>(
            StreamId, r => r.TimestampNormalizado, TimestampNormalizado);
    }

    [Fact]
    public async Task RegistrarMarcacion_PropagaCamposOpcionalesNulos_CuandoElComandoNoLosTrae()
    {
        await WhenAsync(new RegistrarMarcacion(CodigoColaborador, Timestamp, null, null));

        Then(StreamId, CrearMarcacionRegistrada(tipoMarcacion: null, dispositivoId: null));
        ThenIsPublishedPrivately(new PublishOptions { GroupId = CodigoColaborador }, CrearRegistroDeMarcacionCreado(tipoMarcacion: null, dispositivoId: null));
        And<RegistroDeMarcacionAggregateRoot, string?>(StreamId, r => r.TipoMarcacion, null);
        And<RegistroDeMarcacionAggregateRoot, string?>(StreamId, r => r.DispositivoId, null);
    }

    // Duplicado exacto = mismo CodigoColaborador + mismo Timestamp crudo = mismo stream ID: el handler
    // retorna en silencio, sin eventos nuevos ni publicados.
    [Fact]
    public async Task RegistrarMarcacion_NoPersisteNiPublica_CuandoStreamYaExiste()
    {
        Given(StreamId, CrearMarcacionRegistrada());

        await WhenAsync(new RegistrarMarcacion(CodigoColaborador, Timestamp, "ENTRADA", "DEV-001"));

        Then(StreamId);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
        And<RegistroDeMarcacionAggregateRoot, string>(StreamId, r => r.CodigoColaborador, CodigoColaborador);
    }

    // Crear(...) valida el CodigoColaborador: el throw ocurre en el borde del handler (MEF-ADR-0004), no
    // como evento de fallo del aggregate, y antes del event store: ni stream ni publicacion parcial.
    [Fact]
    public async Task RegistrarMarcacion_PropagaArgumentExceptionSinPersistirNiPublicar_CuandoCodigoColaboradorEsVacio()
    {
        var act = async () => await WhenAsync(
            new RegistrarMarcacion(string.Empty, Timestamp, "ENTRADA", "DEV-001"));

        await act.Should().ThrowExactlyAsync<ArgumentException>()
            .WithMessage($"*{MarcacionRegistrada.Mensajes.CodigoColaboradorVacio}*");
        Then(StreamIdSinColaborador);
        ThenIsPublishedPrivately();
        PrivateEventSender.PublishedWithOptions.Should().BeEmpty();
    }
}
