// HU-114: Crear enum Concepto y value objects primitivos del desglose
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects.ControlHoras;

/// <summary>
/// Tests de Concepto - enum de conceptos legales de horas segun legislacion colombiana.
/// CA-1: tiene exactamente 9 valores.
/// </summary>
public class ConceptoTests
{
    // ---------- CA-1: exactamente 9 valores ----------

    [Fact]
    public void Concepto_TieneExactamenteNueveValores()
    {
        var valores = Enum.GetValues<Concepto>();

        valores.Length.Should().Be(9);
    }

    [Fact]
    public void Concepto_ContieneLosTresConceptosOrdinarios()
    {
        var valores = Enum.GetValues<Concepto>().ToHashSet();

        valores.Should().Contain(Concepto.OrdinariaDiurna);
        valores.Should().Contain(Concepto.OrdinariaNocturna);
        valores.Should().Contain(Concepto.Descanso);
    }

    [Fact]
    public void Concepto_ContieneLosCuatroConceptosExtra()
    {
        var valores = Enum.GetValues<Concepto>().ToHashSet();

        valores.Should().Contain(Concepto.ExtraDiurna);
        valores.Should().Contain(Concepto.ExtraNocturna);
        valores.Should().Contain(Concepto.ExtraDiurnaDominicalFestiva);
        valores.Should().Contain(Concepto.ExtraNocturnaDominicalFestiva);
    }

    [Fact]
    public void Concepto_ContieneLosConceptosDominicalFestiva()
    {
        var valores = Enum.GetValues<Concepto>().ToHashSet();

        valores.Should().Contain(Concepto.DominicalFestivaDiurna);
        valores.Should().Contain(Concepto.DominicalFestivaNocturna);
    }
}
