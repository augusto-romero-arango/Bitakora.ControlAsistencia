using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActivarSedeFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class ActivarSedeCommandHandlerTests : CommandHandlerAsyncTest<ActivarSede>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<ActivarSede> Handler =>
        new ActivarSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    [Fact]
    public async Task ActivarSede_EmiteSedeActivada_CuandoLaSedeEstaInactiva()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new SedeDesactivada());

        await WhenAsync(new ActivarSede(Codigo));

        Then(StreamIdEsperado, new SedeActivada());
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, true);
    }

    [Fact]
    public async Task ActivarSede_LanzaReglaDeNegocioDeclinadaException_CuandoLaSedeYaEstaActivaPorNacimiento()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        var act = async () => await WhenAsync(new ActivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ActivarSedeCommandHandler.Mensajes.SedeYaActiva}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, true);
    }

    [Fact]
    public async Task ActivarSede_LanzaReglaDeNegocioDeclinadaException_CuandoLaSedeYaFueReactivadaAntes()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new SedeDesactivada(), new SedeActivada());

        var act = async () => await WhenAsync(new ActivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ActivarSedeCommandHandler.Mensajes.SedeYaActiva}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, true);
    }

    [Fact]
    public async Task ActivarSede_LanzaRecursoNoEncontradoException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new ActivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{ActivarSedeCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
