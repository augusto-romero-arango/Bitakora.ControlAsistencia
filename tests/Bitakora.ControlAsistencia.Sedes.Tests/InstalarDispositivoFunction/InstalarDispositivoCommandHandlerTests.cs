using AwesomeAssertions;
using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
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

    // Sin ubicacion previa por defecto (CA-3): los tests que necesitan cross-sede reasignan antes
    // de WhenAsync, mismo patron que RegistroDeMarcacionCreadoEventHandlerTests.
    private FakeLectorUbicacionDispositivo _lector = new();

    protected override ICommandHandlerAsync<InstalarDispositivo> Handler =>
        new InstalarDispositivoCommandHandler(EventStore, _lector);

    private static SedeRegistrada CrearSedeRegistrada(string codigo = Codigo) =>
        new(codigo, Nombre, null, null);

    // CA-3: nunca instalado -- sin documento UbicacionDispositivo para este dispositivo.
    [Fact]
    public async Task InstalarDispositivo_EmiteDispositivoInstalado_CuandoSedeExiste()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoInstalado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // CA-3: dos dispositivos distintos conviven en la misma sede.
    [Fact]
    public async Task InstalarDispositivo_EmiteDispositivoInstalado_CuandoLaSedeYaTieneOtroDispositivo()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(OtroDispositivoId));

        await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoInstalado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 2);
    }

    // CA-3: reinstalar un dispositivo previamente retirado de esta sede procede -- el retiro deja
    // sin documento vigente en UbicacionDispositivo (la vista solo guarda la ubicacion vigente,
    // sin historial).
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

    // CA-1: la vista ubica al dispositivo en OTRA sede -> 409, sin tocar el aggregate destino.
    [Fact]
    public async Task InstalarDispositivo_LanzaInvalidOperationException_CuandoDispositivoEstaInstaladoEnOtraSede()
    {
        Given(OtroStreamIdEsperado, CrearSedeRegistrada(OtroCodigo), new DispositivoInstalado(DispositivoId));
        Given(StreamIdEsperado, CrearSedeRegistrada());
        _lector = new FakeLectorUbicacionDispositivo(new UbicacionDispositivo(DispositivoId, OtroStreamIdEsperado));

        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.DispositivoInstaladoEnOtraSede}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 0);
    }

    // Nota tecnica del issue: la validacion cross-sede corre ANTES de cargar el aggregate destino
    // (rechazo barato) -- el 409 prevalece aunque la sede destino ni siquiera exista en el store.
    // El And afirma sobre el ORIGEN, no sobre el destino: el destino nunca entra al store en este
    // escenario y And<> exige un aggregate existente (lanza si GetAggregateRoot devuelve null).
    [Fact]
    public async Task InstalarDispositivo_LanzaInvalidOperationException_CuandoDispositivoEstaInstaladoEnOtraSede_AunqueLaSedeDestinoNoExista()
    {
        Given(OtroStreamIdEsperado, CrearSedeRegistrada(OtroCodigo), new DispositivoInstalado(DispositivoId));
        _lector = new FakeLectorUbicacionDispositivo(new UbicacionDispositivo(DispositivoId, OtroStreamIdEsperado));

        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.DispositivoInstaladoEnOtraSede}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(OtroStreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // CA-2: la vista ya ubica al dispositivo en la MISMA sede destino -> la validacion cross-sede
    // no interviene, sigue el flujo actual (declina por YaInstalado, sin emitir).
    [Fact]
    public async Task InstalarDispositivo_LanzaInvalidOperationException_CuandoElDispositivoYaEstaInstaladoEnEstaSede()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(DispositivoId));
        _lector = new FakeLectorUbicacionDispositivo(new UbicacionDispositivo(DispositivoId, StreamIdEsperado));

        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.DispositivoYaInstalado}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // Precondicion de orquestacion (MEF-ADR-0004 capa 2): sede inexistente -> 404, sin escribir
    // nada al event store. Sin ubicacion previa del dispositivo.
    [Fact]
    public async Task InstalarDispositivo_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
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
