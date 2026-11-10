// Payload por rol (CA-ADR-0029 #5): sin esta paridad, un campo nuevo en uno de los dos tipos se pierde
// en silencio en MapearFranja. Las hijas y la sede tipan distinto a proposito en cada isla, por eso la
// paridad va por partes.

using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

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

    // Sede tipa distinto a proposito en cada isla; su paridad de campos la cubre
    // SedeProgramadaParidadConDetalleSedeTests.
    [Fact]
    public void FranjaProgramada_TipaSedeComoSedeProgramadaYDetalleFranjaOrdinariaComoDetalleSede()
    {
        typeof(FranjaProgramada).GetProperty(nameof(FranjaProgramada.Sede))!.PropertyType
            .Should().Be(typeof(SedeProgramada));
        typeof(DetalleFranjaOrdinaria).GetProperty(nameof(DetalleFranjaOrdinaria.Sede))!.PropertyType
            .Should().Be(typeof(DetalleSede));
    }
}
