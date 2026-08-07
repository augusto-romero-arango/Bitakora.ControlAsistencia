// Issue #319 CA-1: guardrail de la duplicacion deliberada de payload por rol (tres islas,
// MEF-ADR-0039 decision 2 y 6): FranjaProgramada (Programacion.DomainEvents) y
// DetalleFranjaOrdinaria (PrivateEvents) declaran el mismo dato en dos ensamblados que no se
// referencian entre si. A diferencia de SubFranjaProgramada, sus hijas (Descansos/Extras) tipan
// DELIBERADAMENTE distinto (SubFranjaProgramada vs DetalleSubFranja) -- por eso la paridad se
// verifica en dos pasos: nombres de propiedad iguales, y las dos propiedades de lista apuntando
// al tipo hijo correcto de cada lado (cuya propia paridad la cubre
// SubFranjaProgramadaParidadConDetalleSubFranjaTests).
//
// Issue #335 (desviacion documentada, MEF-ADR-0012/CA-ADR-0029 decision #5): FranjaProgramada gana
// el campo Sede (payload propio del dominio); el mapeo hacia el bus interno (DetalleFranjaOrdinaria)
// queda explicitamente fuera de alcance de este issue -- ver
// SolicitarProgramacionTurnoCommandHandler.MapearFranja, que no lo propaga todavia. El guardrail de
// paridad exacta se relaja para permitir ESTA UNICA divergencia (Sede); cualquier otro campo nuevo
// que no aparezca en ambos lados sigue rompiendo el test.

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
    public void FranjaProgramada_DeclaraLosMismosNombresDeCampoQueDetalleFranjaOrdinariaMasSedePropia()
    {
        var nombresProgramada = NombresDeCampos<FranjaProgramada>().ToList();
        var nombresDetalle = NombresDeCampos<DetalleFranjaOrdinaria>().ToList();

        nombresProgramada.Where(n => n != nameof(FranjaProgramada.Sede))
            .Should().Equal(nombresDetalle);
        nombresProgramada.Should().Contain(nameof(FranjaProgramada.Sede));
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
}
