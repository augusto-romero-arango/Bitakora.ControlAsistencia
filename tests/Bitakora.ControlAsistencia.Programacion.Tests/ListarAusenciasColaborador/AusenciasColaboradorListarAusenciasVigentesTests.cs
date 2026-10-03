using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.ListarAusenciasColaborador;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ListarAusenciasColaborador;

// Los tramos tras cancelaciones parciales (CA-1) dependen de AusenciaCancelada (#744), que aun no
// existe en el aggregate: aqui se cubre la forma de la vista con los eventos disponibles.
public class AusenciasColaboradorListarAusenciasVigentesTests
{
    private static readonly ColaboradorProgramado Ana = new("CC-1", "E001", "Ana");

    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static AusenciasColaborador ConAusencias(params (Guid Id, int Inicio, int Fin, MotivoAusencia Motivo)[] datos)
    {
        var ac = AusenciasColaborador.Iniciar(new AusenciaProgramada(
            datos[0].Id, Ana, Oct(datos[0].Inicio), Oct(datos[0].Fin), datos[0].Motivo));
        foreach (var d in datos.Skip(1))
            ac.ProgramarAusencia(d.Id, Ana, Oct(d.Inicio), Oct(d.Fin), d.Motivo);
        return ac;
    }

    [Fact]
    public void ListarAusenciasVigentes_EntregaUnTramoConElRangoRegistrado_CuandoNoHayCancelaciones()
    {
        var id = Guid.NewGuid();
        var ac = ConAusencias((id, 13, 26, MotivoAusencia.Vacaciones));

        var vista = ac.ListarAusenciasVigentes(Oct(1), Oct(31));

        vista.Should().Equal(new AusenciaDelColaborador(
            id, MotivoAusencia.Vacaciones, Oct(13), Oct(26), [new TramoVigente(Oct(13), Oct(26))]));
    }

    [Fact]
    public void ListarAusenciasVigentes_EntregaLaAusenciaCompleta_CuandoElRangoSoloLaCruzaParcialmente()
    {
        var id = Guid.NewGuid();
        var ac = ConAusencias((id, 13, 26, MotivoAusencia.Vacaciones));

        var vista = ac.ListarAusenciasVigentes(Oct(20), Oct(28));

        vista.Should().ContainSingle().Which.TramosVigentes
            .Should().Equal(new TramoVigente(Oct(13), Oct(26)));
    }

    [Fact]
    public void ListarAusenciasVigentes_OmiteLasAusenciasQueNoSeCruzanConElRango()
    {
        var dentro = Guid.NewGuid();
        var ac = ConAusencias(
            (Guid.NewGuid(), 1, 3, MotivoAusencia.IncapacidadMedica),
            (dentro, 10, 12, MotivoAusencia.Vacaciones));

        var vista = ac.ListarAusenciasVigentes(Oct(9), Oct(11));

        vista.Select(a => a.Id).Should().Equal(dentro);
    }

    [Fact]
    public void ListarAusenciasVigentes_OrdenaPorFechaInicioAscendente()
    {
        var tarde = Guid.NewGuid();
        var temprano = Guid.NewGuid();
        var ac = ConAusencias(
            (tarde, 20, 22, MotivoAusencia.Vacaciones),
            (temprano, 2, 4, MotivoAusencia.LicenciaRemunerada));

        var vista = ac.ListarAusenciasVigentes(Oct(1), Oct(31));

        vista.Select(a => a.Id).Should().Equal(temprano, tarde);
    }

    [Fact]
    public void ListarAusenciasVigentes_IncluyeLosExtremosDelRango_CuandoCoincidenConLaAusencia()
    {
        var id = Guid.NewGuid();
        var ac = ConAusencias((id, 5, 5, MotivoAusencia.AusenciaNoRemunerada));

        ac.ListarAusenciasVigentes(Oct(5), Oct(5)).Should().ContainSingle();
    }
}
