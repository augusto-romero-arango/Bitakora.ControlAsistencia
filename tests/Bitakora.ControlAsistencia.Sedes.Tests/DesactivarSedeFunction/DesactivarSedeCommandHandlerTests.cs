using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.DesactivarSedeFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class DesactivarSedeCommandHandlerTests : CommandHandlerAsyncTest<DesactivarSede>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<DesactivarSede> Handler =>
        new DesactivarSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    [Fact]
    public async Task DesactivarSede_EmiteSedeDesactivada_CuandoLaSedeEstaActivaPorNacimiento()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new DesactivarSede(Codigo));

        Then(StreamIdEsperado, new SedeDesactivada());
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, false);
    }

    [Fact]
    public async Task DesactivarSede_EmiteSedeDesactivada_CuandoLaSedeFueReactivadaAntes()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new SedeDesactivada(), new SedeActivada());

        await WhenAsync(new DesactivarSede(Codigo));

        Then(StreamIdEsperado, new SedeDesactivada());
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, false);
    }

    [Fact]
    public async Task DesactivarSede_LanzaInvalidOperationException_CuandoLaSedeYaEstaInactiva()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new SedeDesactivada());

        var act = async () => await WhenAsync(new DesactivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{DesactivarSedeCommandHandler.Mensajes.SedeYaInactiva}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, false);
    }

    [Fact]
    public async Task DesactivarSede_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new DesactivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{DesactivarSedeCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
