// Issue #458: asignar (o reemplazar) el centro de costos de una sede. CC opaco -- sin
// interpretacion ni normalizacion, se estampa tal cual (decision de sesion 2026-08-27). CA-ADR-0030:
// sede inexistente se declina con KeyNotFoundException (404); no hay eventos de fallo persistidos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction;
using Bitakora.ControlAsistencia.Sedes.AsignarCentroDeCostosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using Bitakora.ControlAsistencia.Sedes.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.AsignarCentroDeCostosFunction;

// El aggregate usa stream ID compuesto, no el GuidAggregateId del harness -- overloads explicitos
// de Given/Then/And (regla 18 del test-writer).
public class AsignarCentroDeCostosCommandHandlerTests : CommandHandlerAsyncTest<AsignarCentroDeCostos>
{
    private const string Codigo = "SEDE-001";
    private const string Nombre = "Sede Principal";
    private const string CentroDeCostosNuevo = "CC-100";
    private const string CentroDeCostosPrevio = "CC-050";

    // Oraculo independiente de la clave de stream: literal, nunca derivado de ComputarStreamId.
    private const string StreamIdEsperado = "s:SEDE-001";

    protected override ICommandHandlerAsync<AsignarCentroDeCostos> Handler =>
        new AsignarCentroDeCostosCommandHandler(EventStore);

    private static SedeRegistrada CrearSedeRegistrada() => new(Codigo, Nombre, null, null);

    // CA-1: CC valido persiste el string opaco tal cual, sin normalizacion.
    [Fact]
    public async Task AsignarCentroDeCostos_EmiteCentroDeCostosAsignado_CuandoSedeExiste()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada());

        await WhenAsync(new AsignarCentroDeCostos(Codigo, CentroDeCostosNuevo));

        Then(StreamIdEsperado, new CentroDeCostosAsignado(CentroDeCostosNuevo));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, CentroDeCostosNuevo);
    }

    // CA-2: la sede ya tiene un CC vigente -- el mismo comando reemplaza el valor previo, sin
    // distinguir "primera asignacion" de "reemplazo" (PUT semantico).
    [Fact]
    public async Task AsignarCentroDeCostos_EmiteCentroDeCostosAsignado_CuandoLaSedeYaTieneUnCentroVigente()
    {
        Given(StreamIdEsperado, CrearSedeRegistrada(), new CentroDeCostosAsignado(CentroDeCostosPrevio));

        await WhenAsync(new AsignarCentroDeCostos(Codigo, CentroDeCostosNuevo));

        Then(StreamIdEsperado, new CentroDeCostosAsignado(CentroDeCostosNuevo));
        And<SedeAggregateRoot, string?>(StreamIdEsperado, s => s.CentroDeCostos, CentroDeCostosNuevo);
    }

    // CA-5: sede inexistente -> 404 (KeyNotFoundException), sin escribir nada al event store.
    [Fact]
    public async Task AsignarCentroDeCostos_LanzaKeyNotFoundException_CuandoSedeNoExiste()
    {
        var act = async () => await WhenAsync(new AsignarCentroDeCostos(Codigo, CentroDeCostosNuevo));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{AsignarCentroDeCostosCommandHandler.Mensajes.SedeNoEncontrada}*");
        Then(StreamIdEsperado);
    }
}
