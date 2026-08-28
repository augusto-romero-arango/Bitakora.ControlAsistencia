// Issue #458: retirar el centro de costos de una sede -- la sede vuelve a "sin CC". CA-ADR-0030:
// mecanismo "declinar con resultado" -- sin CC vigente se rechaza con InvalidOperationException
// (409, CA-4, propuesta revisable segun el issue); sede inexistente se declina con
// KeyNotFoundException (404). No hay eventos de fallo persistidos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;
using Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarCentroDeCostosFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness -- overloads explicitos
// de Given/Then/And (regla 18 del test-writer).
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

    // CA-3: CC vigente -> persiste CentroDeCostosRetirado; la sede vuelve a "sin CC".
    [Fact]
    public async Task RetirarCentroDeCostos_EmiteCentroDeCostosRetirado_CuandoLaSedeTieneCentroVigente()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new CentroDeCostosAsignado(CentroDeCostosVigente));

        await WhenAsync(new RetirarCentroDeCostos(Codigo));

        Then(StreamIdEsperado, new CentroDeCostosRetirado());
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, null);
    }

    // CA-4: sin CC vigente declina -> 409, ningun evento nuevo, el estado permanece "sin CC".
    [Fact]
    public async Task RetirarCentroDeCostos_LanzaInvalidOperationException_CuandoLaSedeNoTieneCentroVigente()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        var act = async () => await WhenAsync(new RetirarCentroDeCostos(Codigo));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{RetirarCentroDeCostosCommandHandler.Mensajes.SinCentroDeCostosVigente}*");
        Then(StreamIdEsperado);
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, null);
    }

    // CA-5: sede inexistente -> 404 (KeyNotFoundException), sin escribir nada al event store.
    [Fact]
    public async Task RetirarCentroDeCostos_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new RetirarCentroDeCostos(Codigo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{RetirarCentroDeCostosCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
