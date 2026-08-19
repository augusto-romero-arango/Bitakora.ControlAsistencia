// Anatomia de la clave de stream de ControlDiario (CA-ADR-0031 seccion 2). Tests directos sobre el
// metodo estatico puro ComputarStreamId -- sin harness de event sourcing, porque no requiere stream
// ni evento.

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

    // CA-ADR-0031 seccion 2 paso 5: ningun componente puede aportar el separador. Sin el prefijo el
    // split devolveria 2 partes, y una fecha en ISO extendido tampoco alcanzaria para 3 -- este test
    // es lo que impide "simplificar" cualquiera de las dos piezas.
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
