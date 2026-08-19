// Issue #419: renotacion del stream ID de RegistroDeMarcacionAggregateRoot a "rdm:{codigo}:{timestamp}",
// aplicando la heuristica de anatomia de CA-ADR-0031 seccion 2. Tests directos sobre los dos metodos
// estaticos puros del aggregate -- sin harness de event sourcing, porque ninguno de los dos requiere
// stream ni evento.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class RegistroDeMarcacionAggregateRootTests
{
    // CA-1: ejemplo exacto del issue.
    [Fact]
    public void ComputarStreamId_ProduceNotacionConPrefijoRdmYTimestampBasico()
    {
        var streamId = RegistroDeMarcacionAggregateRoot.ComputarStreamId(
            "ABC123", new DateTime(2026, 8, 19, 14, 30, 5));

        streamId.Should().Be("rdm:ABC123:20260819T143005");
    }

    // CA-2 / CA-ADR-0031 seccion 2 paso 5: la clave vigente (ISO extendido) falla este test porque la
    // hora aporta sus propios ':'; la notacion objetivo (ISO basico) lo pasa por construccion.
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

    // CA-3: el comportamiento de EsComponenteValidoDeStreamId no cambia con la renotacion -- sigue
    // rechazando codigos que contengan ':', el separador vigente.
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
