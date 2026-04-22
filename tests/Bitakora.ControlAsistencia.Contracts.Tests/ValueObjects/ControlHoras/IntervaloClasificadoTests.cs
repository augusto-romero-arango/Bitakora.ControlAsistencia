// HU-114: Crear enum Concepto y value objects primitivos del desglose
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Tests de IntervaloClasificado - intervalo temporal con concepto legal asignado.
/// Interfaz publica: constructor primario, Intervalo, Concepto, DuracionEnMinutos.
/// CA-2: DuracionEnMinutos delega al IntervaloTemporal contenido.
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

    // ---------- Igualdad de record (estandar de C#) ----------

    [Fact]
    public void Igualdad_EsTrue_CuandoMismoIntervaloYConcepto()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);
        var a = new IntervaloClasificado(intervalo, Concepto.ExtraDiurna);
        var b = new IntervaloClasificado(intervalo, Concepto.ExtraDiurna);

        a.Should().Be(b);
    }

    [Fact]
    public void Igualdad_EsFalse_CuandoDistintoConcepto()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las17);
        var a = new IntervaloClasificado(intervalo, Concepto.ExtraDiurna);
        var b = new IntervaloClasificado(intervalo, Concepto.OrdinariaDiurna);

        a.Should().NotBe(b);
    }
}
