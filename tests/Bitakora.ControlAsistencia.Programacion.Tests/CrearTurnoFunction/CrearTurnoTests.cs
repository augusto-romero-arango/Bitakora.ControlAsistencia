// Issue #335: tests del mapeo CrearTurno.ToDatosFranjas() -- traduce el contrato HTTP a la
// entrada del factory de TurnoCreado. Cubre la propagacion (o no) de la sede prearmada por franja.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;

public class CrearTurnoTests
{
    private const string NombreTurno = "Turno Manana";

    // CA-1: la sede prearmada de la franja del comando llega a DatosFranja.
    [Fact]
    public void ToDatosFranjas_PropagaLaSedeDeCadaFranja_CuandoComandoTraeSedes()
    {
        var sede = new SedeProgramada("SEDE-SUBA", "Suba");
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno,
            [new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [], sede)]);

        var datos = comando.ToDatosFranjas();

        datos[0].Sede.Should().Be(sede);
    }

    // CA-2: regresion -- una franja sin sede en el comando conserva el comportamiento actual.
    [Fact]
    public void ToDatosFranjas_DejaSedeNull_CuandoFranjaDelComandoNoTraeSede()
    {
        var comando = new CrearTurno(Guid.NewGuid(), NombreTurno,
            [new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

        var datos = comando.ToDatosFranjas();

        datos[0].Sede.Should().BeNull();
    }
}
