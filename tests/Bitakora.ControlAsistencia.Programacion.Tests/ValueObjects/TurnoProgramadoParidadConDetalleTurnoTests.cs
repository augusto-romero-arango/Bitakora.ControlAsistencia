// Guardrail de la duplicacion deliberada de payload por rol (tres islas,
// MEF-ADR-0039 decision 2 y 6): TurnoProgramado (Programacion.DomainEvents) y DetalleTurno
// (PrivateEvents) declaran el mismo dato en dos ensamblados que no se referencian entre si. Su
// hija (FranjasOrdinarias) tipa DELIBERADAMENTE distinto (FranjaProgramada vs
// DetalleFranjaOrdinaria) -- mismo criterio de verificacion en dos pasos que
// FranjaProgramadaParidadConDetalleFranjaOrdinariaTests.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class TurnoProgramadoParidadConDetalleTurnoTests
{
    private static IEnumerable<string> NombresDeCampos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

    [Fact]
    public void TurnoProgramado_DeclaraLosMismosNombresDeCampoQueDetalleTurno()
    {
        NombresDeCampos<TurnoProgramado>().Should().Equal(NombresDeCampos<DetalleTurno>());
    }

    [Fact]
    public void TurnoProgramado_TipaLaMarcaDeHorarioAbiertoComoBool_IgualQueDetalleTurno()
    {
        typeof(TurnoProgramado).GetProperty(nameof(TurnoProgramado.EsHorarioAbierto))!
            .PropertyType.Should().Be(typeof(bool));
        typeof(DetalleTurno).GetProperty(nameof(DetalleTurno.EsHorarioAbierto))!
            .PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void TurnoProgramado_TipaLaAmplitudDeLaVentanaDeEntradaComoInt_IgualQueDetalleTurno()
    {
        typeof(TurnoProgramado).GetProperty(nameof(TurnoProgramado.AmplitudVentanaEntradaEnMinutos))!
            .PropertyType.Should().Be(typeof(int));
        typeof(DetalleTurno).GetProperty(nameof(DetalleTurno.AmplitudVentanaEntradaEnMinutos))!
            .PropertyType.Should().Be(typeof(int));
    }

    [Fact]
    public void TurnoProgramado_TipaFranjasOrdinariasComoListaDeFranjaProgramada()
    {
        typeof(TurnoProgramado).GetProperty(nameof(TurnoProgramado.FranjasOrdinarias))!.PropertyType
            .Should().Be(typeof(IReadOnlyList<FranjaProgramada>));
    }

    [Fact]
    public void TurnoProgramado_TipaLosCamposEscalaresIgualQueDetalleTurno()
    {
        typeof(TurnoProgramado).GetProperty(nameof(TurnoProgramado.Nombre))!.PropertyType
            .Should().Be(typeof(DetalleTurno).GetProperty(nameof(DetalleTurno.Nombre))!.PropertyType);
        typeof(TurnoProgramado).GetProperty(nameof(TurnoProgramado.Descripcion))!.PropertyType
            .Should().Be(typeof(DetalleTurno).GetProperty(nameof(DetalleTurno.Descripcion))!.PropertyType);
    }
}
