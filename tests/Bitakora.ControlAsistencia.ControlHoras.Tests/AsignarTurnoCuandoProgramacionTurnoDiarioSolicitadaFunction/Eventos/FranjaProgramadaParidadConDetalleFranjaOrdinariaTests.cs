// Issue #336 (agregado en revision): guardrail de la duplicacion deliberada de payload por rol
// (tres islas, CA-ADR-0029 decisiones #2 y #5): FranjaProgramada (ControlHoras.DomainEvents) y
// DetalleFranjaOrdinaria (PrivateEvents.Programacion) declaran el mismo dato en dos ensamblados
// que no se referencian entre si. Sin este guardrail, agregar un campo a uno de los dos no rompe
// nada y el dato se pierde en silencio en MapearFranja (el modo de fallo que CA-ADR-0029 decision
// #5 documenta como silencioso). Este par es justamente el que el issue #336 extiende con Sede.
//
// Espejo del guardrail equivalente del lado de Programacion
// (FranjaProgramadaParidadConDetalleFranjaOrdinariaTests, Programacion.Tests, issues #319/#341):
// la paridad se verifica en varios pasos porque las hijas (Descansos/Extras) y la sede tipan
// DELIBERADAMENTE distinto en cada isla -- son gemelos de payload por rol, no el mismo tipo.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

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

    // Issue #336: Sede tipa DELIBERADAMENTE distinto en cada lado (SedeProgramada propia de esta
    // isla vs DetalleSede del bus interno) -- mismo criterio que Descansos/Extras. Su paridad de
    // campos la cubre SedeProgramadaParidadConDetalleSedeTests.
    [Fact]
    public void FranjaProgramada_TipaSedeComoSedeProgramadaYDetalleFranjaOrdinariaComoDetalleSede()
    {
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.Sede))!.PropertyType
            .Should().Be(typeof(SedeProgramada));
        typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.Sede))!.PropertyType
            .Should().Be(typeof(DetalleSede));
    }
}
