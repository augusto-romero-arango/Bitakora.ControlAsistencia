// Issue #420: renotacion del stream ID de ControlDiarioAggregateRoot a "cd:{codigo}:{fecha}",
// aplicando la heuristica de anatomia de CA-ADR-0031 seccion 2 -- mismo patron que #419 aplico a
// RegistroDeMarcacionAggregateRoot. Test directo sobre el metodo estatico puro ComputarStreamId --
// sin harness de event sourcing, porque no requiere stream ni evento.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ControlDiarioAggregateRootTests
{
    // CA-1: ejemplo exacto del issue.
    [Fact]
    public void ComputarStreamId_ProduceNotacionConPrefijoCdYFechaBasica()
    {
        var streamId = ControlDiarioAggregateRoot.ComputarStreamId("ABC123", new DateOnly(2026, 8, 19));

        streamId.Should().Be("cd:ABC123:20260819");
    }

    // CA-2 / CA-ADR-0031 seccion 2 paso 5: la clave vigente (yyyy-MM-dd) falla este test porque la
    // fecha aporta sus propios '-', pero no ':'; sin prefijo el split solo devuelve 2 partes. Con el
    // prefijo "cd" y la fecha en ISO basico (yyyyMMdd, sin separadores propios) el split devuelve
    // exactamente los 3 componentes esperados.
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
