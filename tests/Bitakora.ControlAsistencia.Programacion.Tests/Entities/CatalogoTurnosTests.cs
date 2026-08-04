// Issue #288 CA-3: coherencia entre CatalogoTurnos.ObtenerDetalle().Descripcion y
// CatalogoTurnos.ToString(). El formato de nivel turno ("{nombre} {franjas}") vive en este
// aggregate, en el Function App de Programacion (el worker de proyecciones no puede referenciarlo,
// MEF-ADR-0034 seccion 5). ObtenerDetalle() e Iniciar() son internal (ADR-0015); accesibles en este
// proyecto de tests via InternalsVisibleTo (Bitakora.ControlAsistencia.Programacion.csproj).
// Hoy ObtenerDetalle() todavia asigna un placeholder (fase roja); este test queda en rojo hasta que
// el implementer asigne ToString() en el sitio de produccion (CA-2).
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class CatalogoTurnosTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000099");

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(
            TurnoId,
            "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    [Fact]
    public void ObtenerDetalle_TieneDescripcionCoherenteConToString_CuandoTurnoConUnaOrdinaria()
    {
        var catalogo = CatalogoTurnos.Iniciar(CrearEventoTurno());

        var detalle = catalogo.ObtenerDetalle();

        detalle.Descripcion.Should().Be(catalogo.ToString());
    }
}
