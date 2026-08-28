using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarDispositivoFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class RetirarDispositivoCommandHandlerTests : CommandHandlerAsyncTest<RetirarDispositivo>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string DispositivoId = "DISP-100";
    private const string OtroDispositivoId = "DISP-200";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<RetirarDispositivo> Handler =>
        new RetirarDispositivoCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    // CA-3
    [Fact]
    public async Task RetirarDispositivo_EmiteDispositivoRetirado_CuandoElDispositivoEstaInstalado()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(DispositivoId));

        await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoRetirado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 0);
    }

    // CA-3: retirar un dispositivo deja intactos los demas instalados en la misma sede.
    [Fact]
    public async Task RetirarDispositivo_EmiteDispositivoRetirado_CuandoLaSedeTieneOtroDispositivoInstalado()
    {
        Given(
            StreamIdEsperado,
            CrearSedeRegistrada(),
            new DispositivoInstalado(DispositivoId),
            new DispositivoInstalado(OtroDispositivoId));

        await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoRetirado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // CA-4: declina sin emitir (CA-ADR-0030). El dispositivo-id de ruta direcciona un sub-recurso
    // de una coleccion que no existe en esta sede -> 404, no 409 (a diferencia de
    // RetirarCentroDeCostos, VO singular).
    [Fact]
    public async Task RetirarDispositivo_LanzaKeyNotFoundException_CuandoElDispositivoNoEstaInstaladoEnEstaSede()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        var act = async () => await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarDispositivoCommandHandler.Mensajes.DispositivoNoInstalado}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 0);
    }

    // El retiro no es idempotente hacia arriba: retirar dos veces el mismo dispositivo declina el
    // segundo intento igual que si nunca se hubiera instalado.
    [Fact]
    public async Task RetirarDispositivo_LanzaKeyNotFoundException_CuandoElDispositivoYaFueRetirado()
    {
        Given(
            StreamIdEsperado,
            CrearSedeRegistrada(),
            new DispositivoInstalado(DispositivoId),
            new DispositivoRetirado(DispositivoId));

        var act = async () => await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarDispositivoCommandHandler.Mensajes.DispositivoNoInstalado}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 0);
    }

    // Precondicion de orquestacion (MEF-ADR-0004 capa 2): sede inexistente -> 404, sin escribir
    // nada al event store.
    [Fact]
    public async Task RetirarDispositivo_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarDispositivoCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
