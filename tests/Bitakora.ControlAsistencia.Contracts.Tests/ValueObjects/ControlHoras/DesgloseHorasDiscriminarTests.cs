// Issue #183: DesgloseHoras.Discriminar() produce el payload primitivo HorasDiscriminadas.
// CA-2: MinutosPorConcepto tiene una entrada por cada Concepto con minutos > 0 del dia,
//       clave = Concepto.ToString(), valor = minutos agregados (reusa TotalMinutosPorConcepto).
// CA-3: incluye la clave literal "Retardo" con RetardoTotal.RetardoNeto solo cuando es > 0.
// Trazabilidad queda VACIA en este issue (su generacion es el issue de trazabilidad).
//
// Oraculo independiente (regla absoluta 20, ADR-0002): el desglose rico de entrada y el
// diccionario esperado se construyen a mano con las primitivas del dominio, nunca ejecutando
// la logica bajo prueba (Discriminar/Consolidar/CalcularDesglose).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Tests de DesgloseHoras.Discriminar() - traduccion del desglose rico al payload primitivo.
/// Interfaz publica ejercitada: Discriminar().
/// </summary>
public class DesgloseHorasDiscriminarTests
{
    private static IntervaloTemporal CrearIntervalo(TimeOnly inicio, TimeOnly fin) =>
        IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin));

    private static DetalleFranjaOrdinaria CrearFranjaProgramada() =>
        new(new TimeOnly(8, 0), new TimeOnly(17, 0), 0, [], []);

    // Construye una DesgloseFranja con los intervalos clasificados indicados; su retardo
    // propio es Vacio (el retardo del dia se pasa aparte como RetardoTotal a la DesgloseHoras).
    private static DesgloseFranja CrearFranja(
        params (TimeOnly inicio, TimeOnly fin, Concepto concepto)[] intervalos)
    {
        var clasificados = intervalos
            .Select(i => new IntervaloClasificado(CrearIntervalo(i.inicio, i.fin), i.concepto))
            .ToList<IntervaloClasificado>();
        return new DesgloseFranja(CrearFranjaProgramada(), clasificados, DetalleRetardo.Vacio);
    }

    // RetardoTotal con RetardoNeto = (fin - inicio) minutos, sin compensacion.
    private static DetalleRetardo CrearRetardoNeto(TimeOnly inicio, TimeOnly fin) =>
        DetalleRetardo.Crear([CrearIntervalo(inicio, fin)], []);

    // ---------- CA-2: MinutosPorConcepto vuelca TotalMinutosPorConcepto ----------

    // CA-2: una entrada por cada concepto presente, clave = Concepto.ToString(), valor = minutos.
    [Fact]
    public void Discriminar_VuelcaUnaEntradaPorConcepto_ConClaveYMinutosAgregados()
    {
        // Una franja: 240 min OrdinariaDiurna (08:00-12:00) + 120 min OrdinariaNocturna (04:00-06:00).
        var franja = CrearFranja(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna),
            (new TimeOnly(4, 0), new TimeOnly(6, 0), Concepto.OrdinariaNocturna));
        var desglose = new DesgloseHoras([franja], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["OrdinariaDiurna"] = 240,
            ["OrdinariaNocturna"] = 120
        });
    }

    // CA-2: el mismo concepto en varias franjas se agrega (suma) en una sola entrada.
    [Fact]
    public void Discriminar_SumaMinutosDelMismoConcepto_CuandoApareceEnVariasFranjas()
    {
        var franja1 = CrearFranja((new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var franja2 = CrearFranja((new TimeOnly(13, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja1, franja2], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["OrdinariaDiurna"] = 480
        });
    }

    // CA-2: la clave es exactamente Concepto.ToString() (string), no el valor numerico del enum.
    [Fact]
    public void Discriminar_UsaConceptoToStringComoClave()
    {
        var franja = CrearFranja(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.DominicalFestivaDiurna));
        var desglose = new DesgloseHoras([franja], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().ContainKey(Concepto.DominicalFestivaDiurna.ToString());
        resultado.MinutosPorConcepto.Keys.Should().NotContain("4"); // no el ordinal del enum
    }

    // CA-2: los conceptos ausentes en el desglose no figuran en MinutosPorConcepto.
    [Fact]
    public void Discriminar_OmiteConceptosAusentes_CuandoSoloHayUnConcepto()
    {
        var franja = CrearFranja(
            (new TimeOnly(8, 0), new TimeOnly(17, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().NotContainKey(Concepto.OrdinariaNocturna.ToString());
        resultado.MinutosPorConcepto.Should().NotContainKey(Concepto.ExtraDiurna.ToString());
    }

    // CA-2: desglose vacio (dia sin turno o todas las franjas anomalas) -> MinutosPorConcepto vacio.
    [Fact]
    public void Discriminar_ProduceMinutosPorConceptoVacio_CuandoDesgloseEsVacio()
    {
        var resultado = DesgloseHoras.Vacio.Discriminar();

        resultado.MinutosPorConcepto.Should().BeEmpty();
        resultado.MinutosPorConcepto.Should().NotContainKey("Retardo");
    }

    // ---------- CA-3: clave literal "Retardo" segun RetardoNeto ----------

    // CA-3: con RetardoNeto > 0 aparece la clave "Retardo" con ese valor, junto a los conceptos.
    [Fact]
    public void Discriminar_IncluyeClaveRetardo_CuandoRetardoNetoEsPositivo()
    {
        var franja = CrearFranja(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        // RetardoNeto = 30 min (08:00-08:30) sin compensacion.
        var retardoTotal = CrearRetardoNeto(new TimeOnly(8, 0), new TimeOnly(8, 30));
        var desglose = new DesgloseHoras([franja], retardoTotal, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["OrdinariaDiurna"] = 240,
            ["Retardo"] = 30
        });
    }

    // CA-3: con RetardoNeto = 0 (RetardoTotal vacio) la clave "Retardo" NO aparece.
    [Fact]
    public void Discriminar_OmiteClaveRetardo_CuandoRetardoNetoEsCero()
    {
        var franja = CrearFranja(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var desglose = new DesgloseHoras([franja], DetalleRetardo.Vacio, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().NotContainKey("Retardo");
        resultado.MinutosPorConcepto.Should().ContainKey("OrdinariaDiurna");
    }

    // CA-3: RetardoNeto = 0 por compensacion total (retardado == compensado) tampoco agrega "Retardo".
    [Fact]
    public void Discriminar_OmiteClaveRetardo_CuandoRetardoSeCompensaACero()
    {
        var franja = CrearFranja(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        // 30 min retardados compensados por 30 min -> RetardoNeto = max(0, 30 - 30) = 0.
        var retardoTotal = DetalleRetardo.Crear(
            [CrearIntervalo(new TimeOnly(8, 0), new TimeOnly(8, 30))],
            [CrearIntervalo(new TimeOnly(16, 0), new TimeOnly(16, 30))]);
        var desglose = new DesgloseHoras([franja], retardoTotal, 0);

        var resultado = desglose.Discriminar();

        resultado.MinutosPorConcepto.Should().NotContainKey("Retardo");
    }

    // ---------- Trazabilidad vacia en este issue ----------

    // Issue #183: Trazabilidad queda como lista vacia; su generacion es el issue de trazabilidad.
    [Fact]
    public void Discriminar_DejaTrazabilidadVacia()
    {
        var franja = CrearFranja(
            (new TimeOnly(8, 0), new TimeOnly(12, 0), Concepto.OrdinariaDiurna));
        var retardoTotal = CrearRetardoNeto(new TimeOnly(8, 0), new TimeOnly(8, 30));
        var desglose = new DesgloseHoras([franja], retardoTotal, 1);

        var resultado = desglose.Discriminar();

        resultado.Trazabilidad.Should().BeEmpty();
    }
}
