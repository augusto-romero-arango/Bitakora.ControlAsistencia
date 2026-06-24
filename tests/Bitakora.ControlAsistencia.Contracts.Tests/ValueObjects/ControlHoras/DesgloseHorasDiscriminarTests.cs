// Issue #183: DesgloseHoras.Discriminar() - traduce el desglose rico al payload plano que viaja en
// DiaCalculado (Tell-don't-Ask: el desglose se discrimina a si mismo).
// CA-2: produce MinutosPorConcepto con una entrada por cada Concepto con minutos > 0 del dia,
//       clave = Concepto.ToString(), valor = minutos agregados (reusa TotalMinutosPorConcepto).
// CA-3: incluye la clave literal "Retardo" con RetardoTotal.RetardoNeto cuando es > 0; no la incluye
//       cuando es 0. El enum Concepto permanece SIN un valor Retardo.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

public class DesgloseHorasDiscriminarTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    private static DetalleFranjaOrdinaria FranjaProgramada() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], []);

    // El retardo de la franja no influye en Discriminar (solo lo hace DesgloseHoras.RetardoTotal):
    // las franjas se construyen con DetalleRetardo.Vacio y el retardo del dia se pasa aparte.
    private static DesgloseFranja FranjaConIntervalos(
        params (TimeOnly inicio, TimeOnly fin, Concepto concepto)[] datos)
    {
        var intervalos = datos
            .Select(d => new IntervaloClasificado(CrearIntervalo(d.inicio, d.fin), d.concepto))
            .ToList<IntervaloClasificado>();
        return new DesgloseFranja(FranjaProgramada(), intervalos, DetalleRetardo.Vacio);
    }

    // ---------- CA-2: una entrada por cada Concepto con minutos > 0, clave = Concepto.ToString() ----------

    [Fact]
    public void Discriminar_ProduceUnaEntradaPorConcepto_CuandoVariasFranjas()
    {
        // franja1: 120min OrdinariaNocturna + 240min OrdinariaDiurna; franja2: 240min OrdinariaDiurna.
        // Esperado (oraculo a mano): OrdinariaDiurna = 480, OrdinariaNocturna = 120.
        var franja1 = FranjaConIntervalos(
            (new TimeOnly(6, 0), new TimeOnly(8, 0), Concepto.OrdinariaNocturna),
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = FranjaConIntervalos(
            (new TimeOnly(13, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["OrdinariaDiurna"] = 480,
            ["OrdinariaNocturna"] = 120
        });
    }

    [Fact]
    public void Discriminar_UsaConceptoToStringComoClave_CuandoHayUnConcepto()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.DominicalFestivaDiurna));
        var desglose = new DesgloseHoras([franja], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().ContainKey("DominicalFestivaDiurna");
        resultado.MinutosPorConcepto["DominicalFestivaDiurna"].Should().Be(240);
    }

    [Fact]
    public void Discriminar_OmiteConceptosAusentes_CuandoSoloHayDiurna()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().NotContainKey("ExtraDiurna");
        resultado.MinutosPorConcepto.Should().NotContainKey("OrdinariaNocturna");
    }

    [Fact]
    public void Discriminar_ProduceMinutosPorConceptoVacio_CuandoDesgloseEsVacio()
    {
        var resultado = DesgloseHoras.Vacio.Discriminar();

        resultado.MinutosPorConcepto.Should().BeEmpty();
    }

    // ---------- CA-3: clave literal "Retardo" segun RetardoNeto ----------

    [Fact]
    public void Discriminar_IncluyeClaveRetardo_CuandoRetardoNetoEsMayorACero()
    {
        // Retardo de 15min sin compensacion -> neto 15. Una franja ordinaria de 240min para verificar
        // que la clave "Retardo" convive con las claves de concepto.
        var retardoNeto15 = DetalleRetardo.Crear(
            [CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(6, 15))],
            []);
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], retardoNeto15, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto["Retardo"].Should().Be(15);
        resultado.MinutosPorConcepto["OrdinariaDiurna"].Should().Be(240);
    }

    [Fact]
    public void Discriminar_OmiteClaveRetardo_CuandoRetardoNetoEsCero()
    {
        // Retardo de 30min compensado con 30min -> neto 0. No debe figurar la clave "Retardo".
        var retardoNeto0 = DetalleRetardo.Crear(
            [CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(6, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], retardoNeto0, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().NotContainKey("Retardo");
        resultado.MinutosPorConcepto.Should().ContainKey("OrdinariaDiurna");
    }

    [Fact]
    public void Discriminar_DejaTrazabilidadVacia_EnEsteIssue()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().BeEmpty();
    }

    // CA-3 (guardrail estructural): el enum Concepto NO tiene un valor Retardo; "Retardo" es una clave
    // literal del diccionario, no un concepto del calculo de horas.
    [Fact]
    public void Concepto_NoTieneValorRetardo()
    {
        Enum.GetNames<Concepto>().Should().NotContain("Retardo");
    }
}
