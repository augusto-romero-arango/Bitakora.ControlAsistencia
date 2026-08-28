// Issue #459: activar una sede inactiva. CA-ADR-0030: sede inexistente se declina con
// KeyNotFoundException (404); activar una sede ya activa declina con InvalidOperationException
// (409) via el mecanismo "declinar con resultado" -- sin evento de fallo persistido. La sede nace
// activa (sin evento inicial de activacion, decision de sesion 2026-08-27): CA-3 aplica igual sobre
// una sede recien registrada que sobre una explicitamente reactivada.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActivarSedeFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness -- overloads explicitos
// de Given/Then/And (regla 18 del test-writer).
public class ActivarSedeCommandHandlerTests : CommandHandlerAsyncTest<ActivarSede>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<ActivarSede> Handler =>
        new ActivarSedeCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    // CA-2: sede desactivada + POST -> SedeActivada, la sede queda activa.
    [Fact]
    public async Task ActivarSede_EmiteSedeActivada_CuandoLaSedeEstaInactiva()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new SedeDesactivada());

        await WhenAsync(new ActivarSede(Codigo));

        Then(StreamIdEsperado, new SedeActivada());
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, true);
    }

    // CA-3: la sede nace activa (sin evento inicial) -> activar una sede recien registrada declina
    // igual que activar una ya reactivada, sin emitir ningun evento.
    [Fact]
    public async Task ActivarSede_LanzaInvalidOperationException_CuandoLaSedeYaEstaActivaPorNacimiento()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        var act = async () => await WhenAsync(new ActivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{ActivarSedeCommandHandler.Mensajes.SedeYaActiva}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, true);
    }

    // CA-3: una sede reactivada explicitamente tambien declina ante un segundo ActivarSede.
    [Fact]
    public async Task ActivarSede_LanzaInvalidOperationException_CuandoLaSedeYaFueReactivadaAntes()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new SedeDesactivada(), new SedeActivada());

        var act = async () => await WhenAsync(new ActivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{ActivarSedeCommandHandler.Mensajes.SedeYaActiva}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, bool>(StreamIdEsperado, s => s.Activa, true);
    }

    // CA-5: sede inexistente -> 404 (KeyNotFoundException), sin escribir nada al event store.
    [Fact]
    public async Task ActivarSede_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new ActivarSede(Codigo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{ActivarSedeCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
