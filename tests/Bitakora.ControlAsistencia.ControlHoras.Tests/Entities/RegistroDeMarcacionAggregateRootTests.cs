// Tests directos sobre los dos metodos estaticos puros del aggregate: ninguno requiere stream ni
// evento, asi que no usan el harness de event sourcing.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class RegistroDeMarcacionAggregateRootTests
{
    [Fact]
    public void ComputarStreamId_ProduceNotacionConPrefijoRdmYTimestampBasico()
    {
        var streamId = RegistroDeMarcacionAggregateRoot.ComputarStreamId(
            "ABC123", new DateTime(2026, 8, 19, 14, 30, 5));

        streamId.Should().Be("rdm:ABC123:20260819T143005");
    }

    // CA-ADR-0031 seccion 2 paso 5: en ISO extendido la hora aportaria sus propios ':'; el ISO basico
    // lo evita por construccion.
    [Fact]
    public void ComputarStreamId_ProduceClaveDivisibleEnTresPartes_AlHacerSplitPorElSeparador()
    {
        var streamId = RegistroDeMarcacionAggregateRoot.ComputarStreamId(
            "EMP-001", new DateTime(2026, 3, 15, 8, 9, 0));

        var partes = streamId.Split(':');

        partes.Should().HaveCount(3);
        partes[0].Should().Be("rdm");
        partes[1].Should().Be("EMP-001");
        partes[2].Should().Be("20260315T080900");
    }

    [Fact]
    public void EsComponenteValidoDeStreamId_Rechaza_CuandoCodigoContieneSeparador()
    {
        var esValido = RegistroDeMarcacionAggregateRoot.EsComponenteValidoDeStreamId("EMP:001");

        esValido.Should().BeFalse();
    }

    [Fact]
    public void EsComponenteValidoDeStreamId_Acepta_CuandoCodigoNoContieneSeparador()
    {
        var esValido = RegistroDeMarcacionAggregateRoot.EsComponenteValidoDeStreamId("EMP-001");

        esValido.Should().BeTrue();
    }
}
