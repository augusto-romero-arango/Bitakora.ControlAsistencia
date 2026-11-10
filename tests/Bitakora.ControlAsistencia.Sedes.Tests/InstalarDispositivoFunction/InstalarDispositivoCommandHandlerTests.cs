using AwesomeAssertions;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.InstalarDispositivoFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class InstalarDispositivoCommandHandlerTests : CommandHandlerAsyncTest<InstalarDispositivo>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string DispositivoId = "DISP-100";
    private const string OtroDispositivoId = "DISP-200";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";
    private const string OtroCodigo = "SEDE-002";
    private const string OtroStreamIdEsperado = "s:SEDE-002";

    // Sin ubicacion previa por defecto: los tests cross-sede la reasignan antes de WhenAsync.
    private FakeLectorUbicacionDispositivo _lector = new();

    protected override ICommandHandlerAsync<InstalarDispositivo> Handler =>
        new InstalarDispositivoCommandHandler(EventStore, _lector);

    private static SedeRegistrada CrearSedeRegistrada(string codigo = Codigo) =>
        new(codigo, Nombre, null, null);

    // Nunca instalado: sin documento UbicacionDispositivo.
    [Fact]
    public async Task InstalarDispositivo_EmiteDispositivoInstalado_CuandoSedeExiste()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoInstalado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    [Fact]
    public async Task InstalarDispositivo_EmiteDispositivoInstalado_CuandoLaSedeYaTieneOtroDispositivo()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(OtroDispositivoId));

        await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoInstalado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 2);
    }

    // El retiro deja sin documento en UbicacionDispositivo: la vista solo guarda la ubicacion
    // vigente, sin historial.
    [Fact]
    public async Task InstalarDispositivo_EmiteDispositivoInstalado_CuandoElDispositivoFueRetiradoPreviamente()
    {
        Given(
            StreamIdEsperado,
            CrearSedeRegistrada(),
            new DispositivoInstalado(DispositivoId),
            new DispositivoRetirado(DispositivoId));

        await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoInstalado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // 409 sin tocar el aggregate destino.
    [Fact]
    public async Task InstalarDispositivo_LanzaReglaDeNegocioDeclinadaException_CuandoDispositivoEstaInstaladoEnOtraSede()
    {
        Given(OtroStreamIdEsperado, CrearSedeRegistrada(OtroCodigo), new DispositivoInstalado(DispositivoId));
        Given(StreamIdEsperado, CrearSedeRegistrada());
        _lector = new FakeLectorUbicacionDispositivo(new UbicacionDispositivo(DispositivoId, OtroStreamIdEsperado));

        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.DispositivoInstaladoEnOtraSede}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 0);
    }

    // La validacion cross-sede corre ANTES de cargar el aggregate destino. El And afirma sobre el
    // ORIGEN: el destino nunca entra al store y And<> lanza si el aggregate no existe.
    [Fact]
    public async Task InstalarDispositivo_LanzaReglaDeNegocioDeclinadaException_CuandoDispositivoEstaInstaladoEnOtraSede_AunqueLaSedeDestinoNoExista()
    {
        Given(OtroStreamIdEsperado, CrearSedeRegistrada(OtroCodigo), new DispositivoInstalado(DispositivoId));
        _lector = new FakeLectorUbicacionDispositivo(new UbicacionDispositivo(DispositivoId, OtroStreamIdEsperado));

        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.DispositivoInstaladoEnOtraSede}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(OtroStreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // La validacion cross-sede no interviene: declina por YaInstalado, sin emitir.
    [Fact]
    public async Task InstalarDispositivo_LanzaReglaDeNegocioDeclinadaException_CuandoElDispositivoYaEstaInstaladoEnEstaSede()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(DispositivoId));
        _lector = new FakeLectorUbicacionDispositivo(new UbicacionDispositivo(DispositivoId, StreamIdEsperado));

        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.DispositivoYaInstalado}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // Precondicion de orquestacion (MEF-ADR-0004 capa 2): 404 sin escribir nada al event store.
    [Fact]
    public async Task InstalarDispositivo_LanzaRecursoNoEncontradoException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}

internal sealed class FakeLectorUbicacionDispositivo(UbicacionDispositivo? ubicacion = null)
    : ILectorUbicacionDispositivo
{
    public Task<UbicacionDispositivo?> BuscarUbicacionAsync(string dispositivoId, CancellationToken ct = default) =>
        Task.FromResult(ubicacion);
}
