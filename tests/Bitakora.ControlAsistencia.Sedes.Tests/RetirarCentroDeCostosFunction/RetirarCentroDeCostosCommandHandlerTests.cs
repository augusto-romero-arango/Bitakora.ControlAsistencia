using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;
using Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarCentroDeCostosFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness: Given/Then/And exigen
// los overloads que reciben el streamId explicito.
public class RetirarCentroDeCostosCommandHandlerTests : CommandHandlerAsyncTest<RetirarCentroDeCostos>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string CentroDeCostosVigente = "CC-100";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<RetirarCentroDeCostos> Handler =>
        new RetirarCentroDeCostosCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    // CA-3
    [Fact]
    public async Task RetirarCentroDeCostos_EmiteCentroDeCostosRetirado_CuandoLaSedeTieneCentroVigente()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new CentroDeCostosAsignado(CentroDeCostosVigente));

        await WhenAsync(new RetirarCentroDeCostos(Codigo));

        Then(StreamIdEsperado, new CentroDeCostosRetirado());
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, null);
    }

    // CA-1 (#664): estado ya alcanzado -- MEF-ADR-0004 "Estado ya alcanzado: no-op exitoso". El
    // aggregate declina sin agregar eventos y el handler termina sin lanzar; la ficha no cambia.
    [Fact]
    public async Task RetirarCentroDeCostos_NoEmiteEvento_CuandoLaSedeNoTieneCentroVigente()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new RetirarCentroDeCostos(Codigo));

        Then(StreamIdEsperado);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, null);
    }

    // CA-3 (#664): sede inexistente -> KeyNotFoundException, sin escribir nada al event store.
    [Fact]
    public async Task RetirarCentroDeCostos_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new RetirarCentroDeCostos(Codigo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarCentroDeCostosCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
