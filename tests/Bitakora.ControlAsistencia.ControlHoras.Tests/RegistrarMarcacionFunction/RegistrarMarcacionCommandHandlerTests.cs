// HU-105: Registrar marcacion de entrada o salida

using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.ControlHoras.RegistrarMarcacionFunction.Eventos;
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

    // CA-1, CA-2, CA-5, CA-8: marcacion nueva con todos los campos produce evento y publica internamente
    // CA-2: verifica que 08:09:59 se normaliza a 08:09:00 en el evento emitido
    [Fact]
    public async Task DebeEmitirMarcacionRegistradaYPublicarEvento_CuandoMarcacionEsNueva()
    {
        await WhenAsync(new RegistrarMarcacion(EmpleadoId, Timestamp, "ENTRADA", "DEV-001"));

        Then(StreamId, CrearMarcacionRegistrada());
        ThenIsPublishedPrivately(CrearMarcacionRegistrada());
        And<RegistroDeMarcacionAggregateRoot, string>(StreamId, r => r.EmpleadoId, EmpleadoId);
        And<RegistroDeMarcacionAggregateRoot, DateTime>(
            StreamId, r => r.TimestampNormalizado, TimestampNormalizado);
    }

    // CA-3: TipoMarcacion y DispositivoId son opcionales - null es valido
    [Fact]
    public async Task DebeEmitirMarcacionRegistrada_CuandoCamposOpcionalesSonNulos()
    {
        await WhenAsync(new RegistrarMarcacion(EmpleadoId, Timestamp, null, null));

        Then(StreamId, CrearMarcacionRegistrada(tipoMarcacion: null, dispositivoId: null));
        ThenIsPublishedPrivately(CrearMarcacionRegistrada(tipoMarcacion: null, dispositivoId: null));
        And<RegistroDeMarcacionAggregateRoot, string?>(StreamId, r => r.TipoMarcacion, null);
        And<RegistroDeMarcacionAggregateRoot, string?>(StreamId, r => r.DispositivoId, null);
    }

    // CA-4, CA-9: duplicado exacto (mismo EmpleadoId + mismo Timestamp crudo = mismo stream ID)
    // Handler retorna silenciosamente: sin nuevos eventos en stream, sin eventos publicados
    [Fact]
    public async Task DebeRetornarSilenciosamenteSinPersistirNiPublicar_CuandoStreamYaExiste()
    {
        Given(StreamId, CrearMarcacionRegistrada());

        await WhenAsync(new RegistrarMarcacion(EmpleadoId, Timestamp, "ENTRADA", "DEV-001"));

        Then(StreamId);
        ThenIsPublishedPrivately();
        And<RegistroDeMarcacionAggregateRoot, string>(StreamId, r => r.EmpleadoId, EmpleadoId);
    }
}
