// Issue #456: CA-ADR-0031 paso 5 (test de split simple) -- la anatomia "s:{codigo}" debe devolver
// exactamente los componentes esperados al hacer Split(':'), siempre. Codigo ya es URL-safe
// (rechazado en el borde si no lo es, ver ValidacionesCompartidasSedes): nunca puede contener el
// separador, asi que el split es determinista.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.Entities;

namespace Bitakora.ControlAsistencia.Sedes.Tests.Entities;

public class SedeAggregateRootTests
{
    [Fact]
    public void ComputarStreamId_ProduceExactamenteElPrefijoYElCodigoAlHacerSplit()
    {
        var streamId = SedeAggregateRoot.ComputarStreamId("SEDE-001");

        streamId.Split(':').Should().Equal("s", "SEDE-001");
    }

    [Fact]
    public void ComputarStreamId_AnteponeElPrefijoDeTipoAlCodigo()
    {
        var streamId = SedeAggregateRoot.ComputarStreamId("SEDE-001");

        streamId.Should().Be("s:SEDE-001");
    }
}
