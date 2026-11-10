using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;
using Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AnularTerminacionFunction;

// Corregir una fecha de terminacion errada es anular + terminar de nuevo; aqui se prueba el segundo
// tramo. No se encadenan dos handlers sobre el mismo TestStore: GetAggregateRootAsync solo lee
// _previousEvents, no los _newEvents que commitea el primero, y el segundo veria la vinculacion aun
// terminada (falso 409). Por eso Given incluye el TerminacionAnulada ya persistido.
public class ComposicionAnularYTerminarVinculacionTests : CommandHandlerAsyncTest<TerminarVinculacion>
{
    private const string NumeroValido = "79543210";
    private const string StreamIdEsperado = "CC-79543210";
    private const string CodigoVinculacionVigente = "COL-001";
    private static readonly DateOnly FechaInicioVinculacionVigente = new(2026, 1, 15);
    private static readonly DateOnly FechaEfectivaErrada = new(2026, 6, 1);
    private static readonly DateOnly FechaEfectivaCorregida = new(2026, 6, 5);

    protected override ICommandHandlerAsync<TerminarVinculacion> Handler =>
        new TerminarVinculacionCommandHandler(EventStore);

    private static Identificacion IdentificacionValida() =>
        Identificacion.Crear(TipoIdentificacion.CC, NumeroValido);

    private static NombreColaborador NombreValido() =>
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null);

    [Fact]
    public async Task TerminarVinculacion_EmiteVinculacionTerminadaConLaFechaCorregida_CuandoLaTerminacionErradaFueAnuladaAntes()
    {
        Given(StreamIdEsperado,
            new ColaboradorRegistrado(IdentificacionValida(), NombreValido()),
            new VinculacionIniciada(CodigoVinculacionVigente, FechaInicioVinculacionVigente),
            new VinculacionTerminada(FechaEfectivaErrada),
            new TerminacionAnulada());

        await WhenAsync(new TerminarVinculacion(
            "CC", NumeroValido, CodigoVinculacionVigente, FechaEfectivaCorregida));

        Then(StreamIdEsperado, new VinculacionTerminada(FechaEfectivaCorregida));
        And<ColaboradorAggregateRoot, DateOnly?>(
            StreamIdEsperado, c => c.FechaTerminacionVinculacionVigente, FechaEfectivaCorregida);
        And<ColaboradorAggregateRoot, string>(
            StreamIdEsperado, c => c.CodigoVinculacionVigente, CodigoVinculacionVigente);
    }
}
