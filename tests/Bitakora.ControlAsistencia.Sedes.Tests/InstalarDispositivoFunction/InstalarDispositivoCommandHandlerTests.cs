using AwesomeAssertions;
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

    protected override ICommandHandlerAsync<InstalarDispositivo> Handler =>
        new InstalarDispositivoCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    // CA-1
    [Fact]
    public async Task InstalarDispositivo_EmiteDispositivoInstalado_CuandoSedeExiste()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoInstalado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // CA-1: dos dispositivos distintos conviven en la misma sede.
    [Fact]
    public async Task InstalarDispositivo_EmiteDispositivoInstalado_CuandoLaSedeYaTieneOtroDispositivo()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(OtroDispositivoId));

        await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        Then(StreamIdEsperado, new DispositivoInstalado(DispositivoId));
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 2);
    }

    // CA-6: reinstalar un dispositivo previamente retirado de esta sede procede.
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

    // CA-2: declina sin emitir (CA-ADR-0030) -- exclusividad "a lo sumo en esta sede", sin
    // verificacion cross-sede en v1.
    [Fact]
    public async Task InstalarDispositivo_LanzaInvalidOperationException_CuandoElDispositivoYaEstaInstaladoEnEstaSede()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new DispositivoInstalado(DispositivoId));

        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.DispositivoYaInstalado}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, int>(StreamIdEsperado, s => s.DispositivosInstalados.Count, 1);
    }

    // Precondicion de orquestacion (MEF-ADR-0004 capa 2): sede inexistente -> 404, sin escribir
    // nada al event store.
    [Fact]
    public async Task InstalarDispositivo_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new InstalarDispositivo(Codigo, DispositivoId));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{InstalarDispositivoCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
