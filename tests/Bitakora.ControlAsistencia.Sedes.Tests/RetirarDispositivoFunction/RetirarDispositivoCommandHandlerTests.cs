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

    [Fact]
    public async Task RetirarDispositivo_EmiteDispositivoRetirado_CuandoElDispositivoEstaInstalado()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(DispositivoId));

        await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoRetirado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 0);
    }

    // Retirar un dispositivo deja intactos los demas instalados en la misma sede.
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

    // Estado ya alcanzado (MEF-ADR-0004): un id nunca instalado no se distingue de uno ya
    // retirado -- el aggregate declina sin agregar eventos y el handler termina sin lanzar.
    [Fact]
    public async Task RetirarDispositivo_NoEmiteEvento_CuandoElDispositivoNoEstaInstaladoEnEstaSede()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 0);
    }

    // El retiro SI es idempotente hacia arriba: retirar dos veces el mismo dispositivo es el mismo
    // no-op exitoso que si nunca se hubiera instalado.
    [Fact]
    public async Task RetirarDispositivo_NoEmiteEvento_CuandoElDispositivoYaFueRetirado()
    {
        Given(
            StreamIdEsperado,
            CrearSedeRegistrada(),
            new DispositivoInstalado(DispositivoId),
            new DispositivoRetirado(DispositivoId));

        await WhenAsync(new RetirarDispositivo(Codigo, DispositivoId));

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
