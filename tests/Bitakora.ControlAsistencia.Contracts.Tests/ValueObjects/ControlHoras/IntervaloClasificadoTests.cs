// HU-114: Crear enum Concepto y value objects primitivos del desglose
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Tests de IntervaloClasificado - intervalo temporal con concepto legal asignado.
/// Interfaz publica: constructor primario, Intervalo, Concepto, DuracionEnMinutos, ToString().
/// CA-2: DuracionEnMinutos delega al IntervaloTemporal contenido.
/// Issue #184: ToString() humano para la trazabilidad ("{intervalo}: {etiqueta traducida}") y
/// Mensajes.Etiqueta(Concepto) que resuelve la etiqueta traducida de cada concepto (.resx).
/// </summary>
public class IntervaloClasificadoTests
{
    private static readonly MomentoDelDia Las8 = new(new TimeOnly(8, 0));
    private static readonly MomentoDelDia Las17 = new(new TimeOnly(17, 0));
    private static readonly MomentoDelDia Las22 = new(new TimeOnly(22, 0));
    private static readonly MomentoDelDia Las6SiguienteDia = new(new TimeOnly(6, 0), 1);

    // ---------- CA-2: DuracionEnMinutos delega al IntervaloTemporal ----------

    [Fact]
    public void DuracionEnMinutos_DelegaAlIntervaloTemporal_CuandoRangoDiurno()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);
        var clasificado = new IntervaloClasificado(intervalo, Concepto.OrdinariaDiurna);

        clasificado.DuracionEnMinutos.Should().Be(intervalo.DuracionEnMinutos);
    }

    [Fact]
    public void DuracionEnMinutos_Retorna540_CuandoIntervalo8A17ConOrdinariaDiurna()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);
        var clasificado = new IntervaloClasificado(intervalo, Concepto.OrdinariaDiurna);

        clasificado.DuracionEnMinutos.Should().Be(540);
    }

    [Fact]
    public void DuracionEnMinutos_Retorna480_CuandoIntervaloNocturnoConExtraNocturna()
    {
        var intervalo = IntervaloTemporal.Crear(Las22, Las6SiguienteDia);
        var clasificado = new IntervaloClasificado(intervalo, Concepto.ExtraNocturna);

        clasificado.DuracionEnMinutos.Should().Be(480);
    }

    [Fact]
    public void DuracionEnMinutos_EsIgualAlIntervaloContenido_CuandoConceptoEsDescanso()
    {
        var inicio = new MomentoDelDia(new TimeOnly(10, 0));
        var fin = new MomentoDelDia(new TimeOnly(10, 15));
        var intervalo = IntervaloTemporal.Crear(inicio, fin);
        var clasificado = new IntervaloClasificado(intervalo, Concepto.Descanso);

        clasificado.DuracionEnMinutos.Should().Be(15);
    }

    // ---------- Issue #184: ToString() humano para la trazabilidad ----------

    [Fact]
    public void ToString_RenderizaIntervaloYEtiquetaTraducida_CuandoOrdinariaDiurna()
    {
        // Ejemplo del issue: "18:15-21:00 (165min): Ordinaria diurna".
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(18, 15)), new MomentoDelDia(new TimeOnly(21, 0)));
        var clasificado = new IntervaloClasificado(intervalo, Concepto.OrdinariaDiurna);

        // Oraculo independiente: el intervalo via su ToString() (primitiva probada) y la etiqueta del
        // recurso (no un literal). El codigo "OrdinariaDiurna" no debe aparecer (es la clave, no el texto).
        clasificado.ToString().Should().Be(
            $"{intervalo}: {IntervaloClasificado.Mensajes.Etiqueta(Concepto.OrdinariaDiurna)}");
    }

    [Fact]
    public void ToString_IncluyeLaDuracionDelIntervaloYLaEtiqueta_CuandoNocturnoCruzaMedianoche()
    {
        var intervalo = IntervaloTemporal.Crear(Las22, Las6SiguienteDia);
        var clasificado = new IntervaloClasificado(intervalo, Concepto.ExtraNocturna);

        var texto = clasificado.ToString();

        texto.Should().Contain(intervalo.ToString());  // "22:00-06:00+1 (480min)"
        texto.Should().Contain(IntervaloClasificado.Mensajes.Etiqueta(Concepto.ExtraNocturna));
    }

    // Guardrail .resx: cada Concepto debe tener una etiqueta humana traducida (no vacia). Protege contra
    // agregar un Concepto nuevo sin su traduccion -> Etiqueta devolveria null y la trazabilidad quedaria rota.
    [Fact]
    public void Etiqueta_DefineUnTextoTraducidoNoVacio_ParaCadaConcepto()
    {
        foreach (var concepto in Enum.GetValues<Concepto>())
            IntervaloClasificado.Mensajes.Etiqueta(concepto).Should().NotBeNullOrWhiteSpace();
    }

    // Contrato IEquatable: ver IntervaloClasificadoIgualdadTests (hereda IgualdadTestBase).
}
