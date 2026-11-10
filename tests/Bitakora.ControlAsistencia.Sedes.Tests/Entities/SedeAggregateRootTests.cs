// CA-ADR-0031 paso 5: Split(':') sobre "s:{codigo}" devuelve exactamente sus componentes. Codigo es
// URL-safe (ValidacionesCompartidasSedes lo rechaza en el borde), asi que nunca contiene el separador.

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
