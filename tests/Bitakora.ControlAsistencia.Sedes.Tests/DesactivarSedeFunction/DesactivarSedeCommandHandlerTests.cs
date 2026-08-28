// Issue #459: desactivar una sede activa. CA-ADR-0030: sede inexistente se declina con
// KeyNotFoundException (404); desactivar una sede ya inactiva declina con
// InvalidOperationException (409) via el mecanismo "declinar con resultado" -- sin evento de fallo
// persistido. La sede nace activa (sin evento inicial de activacion): CA-1 sobre una sede recien
// registrada tiene exito sin necesitar ningun SedeActivada previo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.DesactivarSedeFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness -- overloads explicitos
// de Given/Then/And (regla 18 del test-writer).
public class DesactivarSedeCommandHandlerTests : CommandHandlerAsyncTest<DesactivarSede>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<DesactivarSede> Handler =>
        new DesactivarSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    // CA-1: sede recien registrada (nace activa, sin evento inicial) + POST -> SedeDesactivada.
    [Fact]
    public async Task DesactivarSede_EmiteSedeDesactivada_CuandoLaSedeEstaActivaPorNacimiento()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new DesactivarSede(Codigo));

        Then(StreamIdEsperado, new SedeDesactivada());
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, false);
    }

    // CA-1: una sede reactivada explicitamente tambien acepta un nuevo DesactivarSede.
    [Fact]
    public async Task DesactivarSede_EmiteSedeDesactivada_CuandoLaSedeFueReactivadaAntes()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new SedeDesactivada(), new SedeActivada());

        await WhenAsync(new DesactivarSede(Codigo));

        Then(StreamIdEsperado, new SedeDesactivada());
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, false);
    }

    // CA-4: sede ya inactiva -> declina, sin emitir ningun evento nuevo.
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

    // CA-5: sede inexistente -> 404 (KeyNotFoundException), sin escribir nada al event store.
    [Fact]
    public async Task DesactivarSede_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new DesactivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{DesactivarSedeCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
