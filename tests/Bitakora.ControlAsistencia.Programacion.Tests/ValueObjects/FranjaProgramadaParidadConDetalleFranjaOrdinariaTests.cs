// Issue #319 CA-1: guardrail de la duplicacion deliberada de payload por rol (tres islas,
// MEF-ADR-0039 decision 2 y 6): FranjaProgramada (Programacion.DomainEvents) y
// DetalleFranjaOrdinaria (PrivateEvents) declaran el mismo dato en dos ensamblados que no se
// referencian entre si. A diferencia de SubFranjaProgramada, sus hijas (Descansos/Extras) tipan
// DELIBERADAMENTE distinto (SubFranjaProgramada vs DetalleSubFranja) -- por eso la paridad se
// verifica en dos pasos: nombres de propiedad iguales, y las dos propiedades de lista apuntando
// al tipo hijo correcto de cada lado (cuya propia paridad la cubre
// SubFranjaProgramadaParidadConDetalleSubFranjaTests).
//
// Issue #335 abrio una divergencia temporal deliberada (Sede solo en FranjaProgramada, el mapeo
// hacia el bus interno quedaba fuera de alcance). Issue #341 la CIERRA: DetalleFranjaOrdinaria
// gana su propio campo Sede (la sede EFECTIVA que resuelve la cascada) -- el guardrail vuelve a
// exigir paridad EXACTA de nombres de campo en ambos lados, sin excepciones.

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

    // Issue #341: Sede tipa DELIBERADAMENTE distinto en cada lado (SedeProgramada vs DetalleSede,
    // gemelos de payload por rol -- mismo criterio que Descansos/Extras con SubFranjaProgramada vs
    // DetalleSubFranja).
    [Fact]
    public void FranjaProgramada_TipaSedeComoSedeProgramadaYDetalleFranjaOrdinariaComoDetalleSede()
    {
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.Sede))!.PropertyType
            .Should().Be(typeof(SedeProgramada));
        typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.Sede))!.PropertyType
            .Should().Be(typeof(DetalleSede));
    }
}
