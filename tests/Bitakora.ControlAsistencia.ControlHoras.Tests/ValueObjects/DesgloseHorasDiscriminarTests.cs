// Discriminar() traduce el desglose rico al payload plano de DiaDepurado. Las claves de
// HorasPorConcepto son codigo (Concepto.ToString() y la literal "Retardo", presente solo si
// RetardoNeto > 0); solo Trazabilidad es texto humano, una linea por item con valor, construida desde
// los ToString() de los objetos ricos: solo viajan los strings.
//
// Oraculo independiente: las lineas esperadas se arman con IntervaloTemporal.ToString() (primitiva ya
// probada) y la etiqueta del recurso (no un literal), y las horas dividiendo minutos entre 60, nunca
// ejecutando Discriminar ni HorasLiquidables.DesdeMinutos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class DesgloseHorasDiscriminarTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    // Descripcion es un dato derivado, irrelevante aqui: placeholder "".
    private static FranjaProgramada FranjaProgramada() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], [], "");

    // El retardo de la franja no influye en Discriminar (solo lo hace DesgloseHoras.RetardoTotal):
    // las franjas se construyen con Retardo.Vacio y el retardo del dia se pasa aparte.
    private static DesgloseFranja FranjaConIntervalos(
        params (TimeOnly inicio, TimeOnly fin, Concepto concepto)[] datos)
    {
        var intervalos = datos
            .Select(d => new IntervaloClasificado(CrearIntervalo(d.inicio, d.fin), d.concepto))
            .ToList<IntervaloClasificado>();
        return new DesgloseFranja(FranjaProgramada(), intervalos, Retardo.Vacio);
    }

    // Linea de trazabilidad de un concepto con UN solo intervalo: "{intervalo}: {etiqueta traducida}".
    private static string LineaConcepto(IntervaloTemporal intervalo, Concepto concepto) =>
        $"{intervalo}: {IntervaloClasificado.Mensajes.Etiqueta(concepto)}";

    [Fact]
    public void Discriminar_ProduceUnaEntradaPorConcepto_CuandoVariasFranjas()
    {
        // franja1: 120min OrdinariaNocturna + 240min OrdinariaDiurna; franja2: 240min OrdinariaDiurna.
        // Esperado (oraculo a mano, en horas liquidables): OrdinariaDiurna = 480/60 = 8.00,
        // OrdinariaNocturna = 120/60 = 2.00.
        var franja1 = FranjaConIntervalos(
            (new TimeOnly(6, 0), new TimeOnly(8, 0), Concepto.OrdinariaNocturna),
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = FranjaConIntervalos(
            (new TimeOnly(13, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.HorasPorConcepto.Should().BeEquivalentTo(new Dictionary<string, decimal>
        {
            ["OrdinariaDiurna"] = 8.00m,
            ["OrdinariaNocturna"] = 2.00m
        });
    }

    [Fact]
    public void Discriminar_UsaConceptoToStringComoClave_CuandoHayUnConcepto()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.DominicalFestivaDiurna));
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.HorasPorConcepto.Should().ContainKey("DominicalFestivaDiurna");
        resultado.HorasPorConcepto["DominicalFestivaDiurna"].Should().Be(4.00m); // 240min/60
    }

    [Fact]
    public void Discriminar_OmiteConceptosAusentes_CuandoSoloHayDiurna()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.HorasPorConcepto.Should().NotContainKey("ExtraDiurna");
        resultado.HorasPorConcepto.Should().NotContainKey("OrdinariaNocturna");
    }

    [Fact]
    public void Discriminar_ProduceHorasPorConceptoVacio_CuandoDesgloseEsVacio()
    {
        var resultado = DesgloseHoras.Vacio.Discriminar();

        resultado.HorasPorConcepto.Should().BeEmpty();
    }

    [Fact]
    public void Discriminar_IncluyeClaveRetardo_CuandoRetardoNetoEsMayorACero()
    {
        // Retardo de 15min sin compensacion -> neto 15min = 0.25h. Una franja ordinaria de 240min
        // (4.00h) para verificar que la clave "Retardo" convive con las claves de concepto.
        var retardoNeto15 = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(6, 15))],
            []);
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], retardoNeto15, 0);

        var resultado = desglose.Discriminar();

        resultado.HorasPorConcepto["Retardo"].Should().Be(0.25m);
        resultado.HorasPorConcepto["OrdinariaDiurna"].Should().Be(4.00m);
    }

    [Fact]
    public void Discriminar_OmiteClaveRetardo_CuandoRetardoNetoEsCero()
    {
        // Retardo de 30min compensado con 30min -> neto 0. No debe figurar la clave "Retardo".
        var retardoNeto0 = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(6, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], retardoNeto0, 0);

        var resultado = desglose.Discriminar();

        resultado.HorasPorConcepto.Should().NotContainKey("Retardo");
        resultado.HorasPorConcepto.Should().ContainKey("OrdinariaDiurna");
    }

    [Fact]
    public void Discriminar_FormateaLaLineaComoIntervaloYEtiquetaTraducida_CuandoUnSoloIntervalo()
    {
        // "18:15-21:00 (165min): Ordinaria diurna" (18:15-21:00 es todo diurno).
        var franja = FranjaConIntervalos(
            (new TimeOnly(18, 15), new TimeOnly(21, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().ContainSingle()
            .Which.Should().Be(LineaConcepto(
                CrearIntervalo(new TimeOnly(18, 15), new TimeOnly(21, 0)), Concepto.OrdinariaDiurna));
    }

    [Fact]
    public void Discriminar_PueblaUnaLineaPorConcepto_CuandoVariasFranjasYConceptos()
    {
        // OrdinariaDiurna aparece en dos franjas (08:00-12:00 y 13:00-17:00); OrdinariaNocturna en una.
        var franja1 = FranjaConIntervalos(
            (new TimeOnly(6, 0), new TimeOnly(8, 0), Concepto.OrdinariaNocturna),
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = FranjaConIntervalos(
            (new TimeOnly(13, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        // Una linea por concepto (no por intervalo): 2 conceptos -> 2 lineas.
        resultado.Trazabilidad.Should().HaveCount(2);

        // La linea de OrdinariaDiurna referencia sus DOS intervalos y una sola vez la etiqueta. El formato
        // exacto del join es libre; aqui se verifica el contenido obligatorio.
        var diurna1 = CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0));
        var diurna2 = CrearIntervalo(new TimeOnly(13, 0), new TimeOnly(17, 0));
        var lineaDiurna = resultado.Trazabilidad.Should()
            .ContainSingle(l => l.Contains(IntervaloClasificado.Mensajes.Etiqueta(Concepto.OrdinariaDiurna)))
            .Which;
        lineaDiurna.Should().Contain(diurna1.ToString()).And.Contain(diurna2.ToString());

        var nocturna = CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var lineaNocturna = resultado.Trazabilidad.Should()
            .ContainSingle(l => l.Contains(IntervaloClasificado.Mensajes.Etiqueta(Concepto.OrdinariaNocturna)))
            .Which;
        lineaNocturna.Should().Contain(nocturna.ToString());
    }

    [Fact]
    public void Discriminar_ProduceUnaLineaPorEntradaDeHorasPorConcepto_SinRetardo()
    {
        // Una linea por item con valor: sin retardo, Trazabilidad.Count == HorasPorConcepto.Count.
        var franja1 = FranjaConIntervalos(
            (new TimeOnly(6, 0), new TimeOnly(8, 0), Concepto.OrdinariaNocturna),
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = FranjaConIntervalos(
            (new TimeOnly(13, 0), new TimeOnly(15, 0), Concepto.ExtraDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().HaveCount(resultado.HorasPorConcepto.Count);
        resultado.Trazabilidad.Should().HaveCount(3);
    }

    [Fact]
    public void Discriminar_DejaTrazabilidadVacia_CuandoDesgloseEsVacio()
    {
        // Edge: sin conceptos ni retardo no hay memoria de calculo que mostrar.
        var resultado = DesgloseHoras.Vacio.Discriminar();

        resultado.Trazabilidad.Should().BeEmpty();
    }

    [Fact]
    public void Discriminar_TraduceLaTrazabilidadPeroDejaLaClaveComoCodigo_CuandoUnConcepto()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.DominicalFestivaDiurna));
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        // La clave de HorasPorConcepto es el codigo estable Concepto.ToString().
        resultado.HorasPorConcepto.Should().ContainKey("DominicalFestivaDiurna");
        // La trazabilidad lleva el texto humano traducido del recurso, no el codigo camelCase.
        resultado.Trazabilidad.Should().ContainSingle()
            .Which.Should().Contain(IntervaloClasificado.Mensajes.Etiqueta(Concepto.DominicalFestivaDiurna));
        string.Join(" | ", resultado.Trazabilidad).Should().NotContain("DominicalFestivaDiurna");
    }

    [Fact]
    public void Discriminar_NoIncluyeLineasParaConceptosAusentes_CuandoSoloHayDiurna()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().ContainSingle();  // solo la linea de OrdinariaDiurna
        var todas = string.Join(" | ", resultado.Trazabilidad);
        todas.Should().NotContain(IntervaloClasificado.Mensajes.Etiqueta(Concepto.ExtraDiurna));
        todas.Should().NotContain(IntervaloClasificado.Mensajes.Etiqueta(Concepto.OrdinariaNocturna));
    }

    [Fact]
    public void Discriminar_IncluyeLineaDeRetardoDerivadaDeRetardoToString_CuandoRetardoNetoEsMayorACero()
    {
        // Retardo de 15min sin compensacion -> neto 15. Retardo.ToString() ya viene traducido por sus
        // propios .resx; la linea de trazabilidad del retardo se deriva de el.
        var retardo = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(6, 15))],
            []);
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], retardo, 0);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().Contain(retardo.ToString());
        // Convive con la linea del concepto.
        resultado.Trazabilidad.Should().Contain(LineaConcepto(
            CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0)), Concepto.OrdinariaDiurna));
    }

    [Fact]
    public void Discriminar_OmiteLineaDeRetardo_CuandoRetardoNetoEsCero()
    {
        // Retardo de 30min compensado con 30min -> neto 0. No hay clave "Retardo" ni linea de retardo.
        var retardo = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(6, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], retardo, 0);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().NotContain(retardo.ToString());
        resultado.Trazabilidad.Should().ContainSingle()
            .Which.Should().Be(LineaConcepto(
                CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(12, 0)), Concepto.OrdinariaDiurna));
    }

    // El enum Concepto NO tiene un valor Retardo: "Retardo" es una clave literal del diccionario, no un
    // concepto del calculo de horas.
    [Fact]
    public void Concepto_NoTieneValorRetardo()
    {
        Enum.GetNames<Concepto>().Should().NotContain("Retardo");
    }
}
