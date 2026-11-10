// Guardrail de la duplicacion deliberada de payload por rol (tres islas, MEF-ADR-0039):
// FranjaProgramada (Programacion.DomainEvents) y DetalleFranjaOrdinaria (PrivateEvents) declaran el
// mismo dato en dos ensamblados que no se referencian entre si. Sus hijas (Descansos/Extras) tipan
// DELIBERADAMENTE distinto (SubFranjaProgramada vs DetalleSubFranja), por eso la paridad se verifica
// en dos pasos: nombres de propiedad iguales (paridad EXACTA, sin excepciones) y cada lista apuntando
// al tipo hijo correcto de su lado (cuya paridad cubre SubFranjaProgramadaParidadConDetalleSubFranjaTests).

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class FranjaProgramadaParidadConDetalleFranjaOrdinariaTests
{
    private static IEnumerable<string> NombresDeCampos<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

    [Fact]
    public void FranjaProgramada_DeclaraLosMismosNombresDeCampoQueDetalleFranjaOrdinaria()
    {
        NombresDeCampos<FranjaProgramada>().Should().Equal(NombresDeCampos<DetalleFranjaOrdinaria>());
    }

    [Fact]
    public void FranjaProgramada_TipaDescansosYExtrasComoListasDeSubFranjaProgramada()
    {
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.Descansos))!.PropertyType
            .Should().Be(typeof(IReadOnlyList<SubFranjaProgramada>));
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.Extras))!.PropertyType
            .Should().Be(typeof(IReadOnlyList<SubFranjaProgramada>));
    }

    [Fact]
    public void FranjaProgramada_TipaLosCamposEscalaresIgualQueDetalleFranjaOrdinaria()
    {
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.HoraInicio))!.PropertyType
            .Should().Be(typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.HoraInicio))!.PropertyType);
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.HoraFin))!.PropertyType
            .Should().Be(typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.HoraFin))!.PropertyType);
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.DiaOffsetFin))!.PropertyType
            .Should().Be(typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.DiaOffsetFin))!.PropertyType);
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.Descripcion))!.PropertyType
            .Should().Be(typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.Descripcion))!.PropertyType);
    }

    // Sede tipa DELIBERADAMENTE distinto en cada lado (SedeProgramada vs DetalleSede, gemelos de
    // payload por rol), como Descansos/Extras.
    [Fact]
    public void FranjaProgramada_TipaSedeComoSedeProgramadaYDetalleFranjaOrdinariaComoDetalleSede()
    {
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.Sede))!.PropertyType
            .Should().Be(typeof(SedeProgramada));
        typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.Sede))!.PropertyType
            .Should().Be(typeof(DetalleSede));
    }
}
