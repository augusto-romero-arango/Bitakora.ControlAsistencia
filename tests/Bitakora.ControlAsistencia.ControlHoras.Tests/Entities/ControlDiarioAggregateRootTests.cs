using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ControlDiarioAggregateRootTests
{
    [Fact]
    public void ComputarStreamId_ProduceNotacionConPrefijoCdYFechaBasica()
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId("ABC123", new DateOnly(2026, 8, 19));

        streamId.Should().Be("cd:ABC123:20260819");
    }

    // Ningun componente puede aportar el separador (CA-ADR-0031 seccion 2): este test impide quitar el
    // prefijo o pasar la fecha a ISO extendido.
    [Fact]
    public void ComputarStreamId_ProduceClaveDivisibleEnTresPartes_AlHacerSplitPorElSeparador()
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId("EMP-001", new DateOnly(2026, 3, 15));

        var partes = streamId.Split(':');

        partes.Should().HaveCount(3);
        partes[0].Should().Be("cd");
        partes[1].Should().Be("EMP-001");
        partes[2].Should().Be("20260315");
    }
}
