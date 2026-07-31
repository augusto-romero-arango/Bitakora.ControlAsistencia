// Issue #277 CA-1: IdentidadEventosProgramacion.TiposPersistidos debe listar exactamente los
// tipos que se persisten en el event store de Programacion -- ni de mas (ningun evento de bus),
// ni de menos (el olvido que este issue corrige). No usa el harness Given/When/Then: no hay
// aggregate ni command handler involucrado, es un dato estatico que consume ComposicionServicios
// (write-side) y ConfiguracionMartenProjectionsProgramacion (read-side).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class IdentidadEventosProgramacionTests
{
    [Fact]
    public void TiposPersistidos_ContieneExactamenteLosDosEventosPersistidosDeProgramacion()
    {
        IdentidadEventosProgramacion.TiposPersistidos.Should().BeEquivalentTo(
        [
            typeof(TurnoCreado),
            typeof(ProgramacionTurnoSolicitada)
        ]);
    }
}
