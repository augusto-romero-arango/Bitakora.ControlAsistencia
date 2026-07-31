// Issue #277 CA-1: IdentidadEventosControlHoras.TiposPersistidos debe listar exactamente los
// tipos que se persisten en el event store de ControlHoras -- ni de mas (ningun evento que solo
// cruce el bus, p.ej. DiaCalculado o ProgramacionTurnoDiarioSolicitada), ni de menos (el olvido
// que este issue corrige). MarcacionRegistrada SI entra: ademas de IPrivateEvent, se persiste en
// el stream de ControlDiarioAggregateRoot. No usa el harness Given/When/Then: es un dato
// estatico, no hay aggregate ni command handler involucrado.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Infraestructura;

public class IdentidadEventosControlHorasTests
{
    [Fact]
    public void TiposPersistidos_ContieneExactamenteLosTresEventosPersistidosDeControlHoras()
    {
        IdentidadEventosControlHoras.TiposPersistidos.Should().BeEquivalentTo(
        [
            typeof(MarcacionRegistrada),
            typeof(MarcacionAdicionada),
            typeof(TurnoDiarioAsignado)
        ]);
    }
}
