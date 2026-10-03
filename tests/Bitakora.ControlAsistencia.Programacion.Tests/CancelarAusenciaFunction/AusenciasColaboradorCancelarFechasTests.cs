using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CancelarAusenciaFunction;

public class AusenciasColaboradorCancelarFechasTests
{
    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000000744");
    private static readonly ColaboradorProgramado Ana = new("CC-1", "E001", "Ana");

    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static AusenciasColaborador Con13Al26() =>
        AusenciasColaborador.Iniciar(
            new AusenciaProgramada(AusenciaId, Ana, Oct(13), Oct(26), MotivoAusencia.IncapacidadMedica));

    private static string Tramos(AusenciasColaborador a) =>
        string.Join(",", a.ListarAusenciasVigentes(Oct(1), Oct(31)).Single().TramosVigentes
            .Select(t => $"{t.Desde.Day}-{t.Hasta.Day}"));

    [Fact]
    public void CancelarFechas_RetornaCanceladasConLasVigentes_CuandoLaPeticionMezclaVigentesYAjenas()
    {
        var ac = Con13Al26();

        var resultado = ac.CancelarFechas(AusenciaId, [Oct(25), Oct(26), Oct(27), Oct(28)]);

        var canceladas = resultado.Should().BeOfType<ResultadoCancelarAusencia.Canceladas>().Subject;
        canceladas.Fechas.Should().Equal(Oct(25), Oct(26));
        canceladas.Colaborador.Should().Be(Ana);
        Tramos(ac).Should().Be("13-24");
    }

    [Fact]
    public void CancelarFechas_PartePorTramos_CuandoSeCancelaUnaFechaDelMedio()
    {
        var ac = Con13Al26();

        ac.CancelarFechas(AusenciaId, [Oct(20), Oct(21), Oct(22)]);

        Tramos(ac).Should().Be("13-19,23-26");
    }

    [Fact]
    public void CancelarFechas_RetornaSinCambios_CuandoLasFechasYaEstabanCanceladas()
    {
        var ac = Con13Al26();
        ac.CancelarFechas(AusenciaId, [Oct(20)]);

        var resultado = ac.CancelarFechas(AusenciaId, [Oct(20)]);

        resultado.Should().BeOfType<ResultadoCancelarAusencia.SinCambios>();
        Tramos(ac).Should().Be("13-19,21-26");
    }

    [Fact]
    public void CancelarFechas_RetornaSinCambios_CuandoLasFechasSonAjenasALaAusencia()
    {
        var ac = Con13Al26();

        var resultado = ac.CancelarFechas(AusenciaId, [Oct(1), Oct(30)]);

        resultado.Should().BeOfType<ResultadoCancelarAusencia.SinCambios>();
        Tramos(ac).Should().Be("13-26");
    }

    [Fact]
    public void CancelarFechas_RetornaAusenciaInexistente_CuandoElIdNoEstaEnElStream()
    {
        var ac = Con13Al26();

        var resultado = ac.CancelarFechas(Guid.Parse("019600a0-0000-7000-8000-000000000999"), [Oct(20)]);

        resultado.Should().BeOfType<ResultadoCancelarAusencia.AusenciaInexistente>();
        Tramos(ac).Should().Be("13-26");
    }
}
