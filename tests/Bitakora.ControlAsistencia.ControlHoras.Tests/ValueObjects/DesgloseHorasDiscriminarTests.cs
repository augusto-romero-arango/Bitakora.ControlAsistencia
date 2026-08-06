// Issue #183: DesgloseHoras.Discriminar() - traduce el desglose rico al payload plano que viaja en
// DiaCalculado (Tell-don't-Ask: el desglose se discrimina a si mismo).
// CA-2 (#183): produce MinutosPorConcepto con una entrada por cada Concepto con minutos > 0 del dia,
//       clave = Concepto.ToString(), valor = minutos agregados (reusa TotalMinutosPorConcepto).
// CA-3 (#183): incluye la clave literal "Retardo" con RetardoTotal.RetardoNeto cuando es > 0; no la incluye
//       cuando es 0. El enum Concepto permanece SIN un valor Retardo.
//
// Issue #184: Discriminar() ahora ademas puebla Trazabilidad (la memoria de calculo). Decision del planner
// (2026-06-23): lista de lineas (IReadOnlyList<string>), una por item con valor, ya traducida en el back.
// CA-1 (#184): una linea por concepto con minutos > 0, armada desde el/los IntervaloTemporal.ToString() del
//       concepto y su etiqueta humana traducida (IntervaloClasificado.Mensajes.Etiqueta). Ej (issue):
//       "18:15-21:00 (165min): Ordinaria diurna".
// CA-2 (#184): incluye una linea para el retardo cuando RetardoNeto > 0, derivada de Retardo.ToString().
// CA-3 (#184): las lineas estan traducidas (.resx); no hay lineas para items en cero.
// CA-4 (#184): las claves de MinutosPorConcepto siguen como codigo ("OrdinariaDiurna", "Retardo"); solo
//       Trazabilidad es texto humano.
// "El modelo de dominio rico no cruza el bus": la trazabilidad se construye DESDE los ToString() de los
// objetos ricos (IntervaloTemporal, Retardo); solo viajan los strings, no los objetos ricos.
// Oraculo independiente (regla 20): las lineas esperadas se arman a mano con IntervaloTemporal.ToString()
// (primitiva ya probada) y la etiqueta del recurso (no un literal), nunca ejecutando Discriminar.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

public class DesgloseHorasDiscriminarTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    // Issue #288: Descripcion (dato derivado) es irrelevante para estos tests -> placeholder "".
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

    // Oraculo independiente de la linea de trazabilidad de un concepto con UN solo intervalo:
    // "{intervalo}: {etiqueta traducida}". Se arma desde IntervaloTemporal.ToString() (primitiva probada)
    // y la etiqueta del recurso (no un literal), sin ejecutar Discriminar (regla 20).
    private static string LineaConcepto(IntervaloTemporal intervalo, Concepto concepto) =>
        $"{intervalo}: {IntervaloClasificado.Mensajes.Etiqueta(concepto)}";

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
        var desglose = new DesgloseHoras([franja1, franja2], Retardo.Vacio, 0);

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
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().ContainKey("DominicalFestivaDiurna");
        resultado.MinutosPorConcepto["DominicalFestivaDiurna"].Should().Be(240);
    }

    [Fact]
    public void Discriminar_OmiteConceptosAusentes_CuandoSoloHayDiurna()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

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
        var retardoNeto15 = Retardo.Crear(
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
        var retardoNeto0 = Retardo.Crear(
            [CrearIntervalo(new TimeOnly(6, 0), new TimeOnly(6, 30))],
            [CrearIntervalo(new TimeOnly(12, 0), new TimeOnly(12, 30))]);
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], retardoNeto0, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().NotContainKey("Retardo");
        resultado.MinutosPorConcepto.Should().ContainKey("OrdinariaDiurna");
    }

    // ---------- Issue #184 - CA-1: una linea por concepto con minutos > 0 ----------

    [Fact]
    public void Discriminar_FormateaLaLineaComoIntervaloYEtiquetaTraducida_CuandoUnSoloIntervalo()
    {
        // Ejemplo literal del issue: "18:15-21:00 (165min): Ordinaria diurna" (18:15-21:00 es todo diurno).
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

        // CA-1: una linea por concepto (no por intervalo): 2 conceptos -> 2 lineas.
        resultado.Trazabilidad.Should().HaveCount(2);

        // La linea de OrdinariaDiurna referencia sus DOS intervalos y su etiqueta traducida (una sola vez
        // la etiqueta -> es una linea por concepto, no por intervalo). Formato exacto del join a criterio
        // del implementer; aqui se verifica el contenido obligatorio.
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
    public void Discriminar_ProduceUnaLineaPorEntradaDeMinutosPorConcepto_SinRetardo()
    {
        // Decision del planner: "una linea por item con valor". Sin retardo, los items son los conceptos,
        // asi que Trazabilidad.Count == MinutosPorConcepto.Count.
        var franja1 = FranjaConIntervalos(
            (new TimeOnly(6, 0), new TimeOnly(8, 0), Concepto.OrdinariaNocturna),
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = FranjaConIntervalos(
            (new TimeOnly(13, 0), new TimeOnly(15, 0), Concepto.ExtraDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().HaveCount(resultado.MinutosPorConcepto.Count);
        resultado.Trazabilidad.Should().HaveCount(3);
    }

    [Fact]
    public void Discriminar_DejaTrazabilidadVacia_CuandoDesgloseEsVacio()
    {
        // Edge: sin conceptos ni retardo no hay memoria de calculo que mostrar.
        var resultado = DesgloseHoras.Vacio.Discriminar();

        resultado.Trazabilidad.Should().BeEmpty();
    }

    // ---------- Issue #184 - CA-3 / CA-4: traducida pero claves como codigo ----------

    [Fact]
    public void Discriminar_TraduceLaTrazabilidadPeroDejaLaClaveComoCodigo_CuandoUnConcepto()
    {
        var franja = FranjaConIntervalos(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.DominicalFestivaDiurna));
        var desglose = new DesgloseHoras([franja], Retardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        // CA-4: la clave de MinutosPorConcepto es el codigo estable Concepto.ToString().
        resultado.MinutosPorConcepto.Should().ContainKey("DominicalFestivaDiurna");
        // CA-3: la trazabilidad lleva el texto humano traducido del recurso, no el codigo camelCase.
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

    // ---------- Issue #184 - CA-2: linea de retardo derivada de Retardo.ToString() ----------

    [Fact]
    public void Discriminar_IncluyeLineaDeRetardoDerivadaDeRetardoToString_CuandoRetardoNetoEsMayorACero()
    {
        // Retardo de 15min sin compensacion -> neto 15. Retardo.ToString() ya viene traducido por sus
        // propios .resx; la linea de trazabilidad del retardo se deriva de el (CA-2).
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

    // CA-3 (guardrail estructural): el enum Concepto NO tiene un valor Retardo; "Retardo" es una clave
    // literal del diccionario, no un concepto del calculo de horas.
    [Fact]
    public void Concepto_NoTieneValorRetardo()
    {
        Enum.GetNames<Concepto>().Should().NotContain("Retardo");
    }
}
