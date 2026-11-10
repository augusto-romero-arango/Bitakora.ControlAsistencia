using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class DesgloseHorasTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    // Descripcion es un dato derivado, irrelevante aqui: placeholder "".
    private static FranjaProgramada CrearFranjaProgramada() =>
        new FranjaProgramada(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], [], "");

    private static DesgloseFranja CrearFranjaConIntervalos(
        params (TimeOnly inicio, TimeOnly fin, Concepto concepto)[] datos)
    {
        var intervalos = datos
            .Select(d => new IntervaloClasificado(CrearIntervalo(d.inicio, d.fin), d.concepto))
            .ToList<IntervaloClasificado>();
        return new DesgloseFranja(CrearFranjaProgramada(), intervalos, Retardo.Vacio);
    }

    [Fact]
    public void TotalMinutosPorConcepto_EsLaSumaDeTodasLasFranjas_CuandoMismoConcepto()
    {
        // franja1: 240 min OrdinariaDiurna, franja2: 240 min OrdinariaDiurna => total 480
        var franja1 = CrearFranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = CrearFranjaConIntervalos(
            (new TimeOnly(13, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], Retardo.Vacio, 0);

        desglose.TotalMinutosPorConcepto[Concepto.OrdinariaDiurna].Should().Be(480);
    }

    [Fact]
    public void TotalMinutosPorConcepto_SumaConceptosParciales_CuandoConceptoApareceEnAlgunasFranjas()
    {
        // franja1: 120 min nocturno + 240 min diurno; franja2: 240 min diurno
        // Esperado: nocturno=120, diurno=480
        var franja1 = CrearFranjaConIntervalos(
            (new TimeOnly(6, 0), new TimeOnly(8, 0), Concepto.OrdinariaNocturna),
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = CrearFranjaConIntervalos(
            (new TimeOnly(13, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], Retardo.Vacio, 0);

        desglose.TotalMinutosPorConcepto[Concepto.OrdinariaNocturna].Should().Be(120);
        desglose.TotalMinutosPorConcepto[Concepto.OrdinariaDiurna].Should().Be(480);
    }

    [Fact]
    public void TotalMinutosPorConcepto_OmiteConceptoAusenteEnTodasLasFranjas_CuandoSoloHayDiurna()
    {
        var franja1 = CrearFranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja1], Retardo.Vacio, 0);

        desglose.TotalMinutosPorConcepto.Should().NotContainKey(Concepto.ExtraDiurna);
        desglose.TotalMinutosPorConcepto.Should().NotContainKey(Concepto.OrdinariaNocturna);
    }

    [Fact]
    public void TotalMinutosPorConcepto_EstaVacio_CuandoSinFranjas()
    {
        var desglose = new DesgloseHoras([], Retardo.Vacio, 0);

        desglose.TotalMinutosPorConcepto.Should().BeEmpty();
    }

    [Fact]
    public void Vacio_TieneDesglosePorFranjaVacio()
    {
        DesgloseHoras.Vacio.DesglosePorFranja.Should().BeEmpty();
    }

    [Fact]
    public void Vacio_TieneRetardoTotalIgualARetardoVacio()
    {
        DesgloseHoras.Vacio.RetardoTotal.Should().Be(Retardo.Vacio);
    }

    [Fact]
    public void Vacio_TieneFranjasAnomalasEnCero()
    {
        DesgloseHoras.Vacio.FranjasAnomalas.Should().Be(0);
    }

    [Fact]
    public void Vacio_TieneTotalMinutosPorConceptoVacio()
    {
        DesgloseHoras.Vacio.TotalMinutosPorConcepto.Should().BeEmpty();
    }
}
