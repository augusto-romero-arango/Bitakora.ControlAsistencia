using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class PuntajeDeHorarioTests
{
    private static FranjaFicha Franja(string inicio, string fin, int diaOffsetFin = 0) =>
        new(TimeOnly.Parse(inicio), TimeOnly.Parse(fin), diaOffsetFin, [], [], null, null, "");

    private static CoincidenciaDeHorario? Evaluar(string pedido, params FranjaFicha[] franjas)
    {
        PuntajeDeHorario.TryParsear(pedido, out var puntaje).Should().BeTrue();
        return puntaje!.Evaluar(franjas);
    }

    [Fact]
    public void Evaluar_EsExactoConDistanciaCero_CuandoElRangoCoincideConLaFranja()
    {
        var resultado = Evaluar("06:00-14:00", Franja("06:00", "14:00"));

        resultado!.Tipo.Should().Be(TipoCoincidenciaDeHorario.Exacto);
        resultado.DistanciaEnMinutos.Should().Be(0);
    }

    [Fact]
    public void Evaluar_PonePrimeroLaFranjaMasCercana_CuandoSeComparanDosFranjasQueSeCruzan()
    {
        var cercana = Evaluar("06:00-14:00", Franja("05:00", "13:00"));
        var lejana = Evaluar("06:00-14:00", Franja("05:00", "08:00"));

        cercana!.DistanciaEnMinutos.Should().Be(120);
        lejana!.DistanciaEnMinutos.Should().Be(420);
        cercana.DistanciaEnMinutos.Should().BeLessThan(lejana.DistanciaEnMinutos);
    }

    [Fact]
    public void Evaluar_NoEntra_CuandoLaFranjaNoSeCruzaConElRango()
    {
        var resultado = Evaluar("06:00-14:00", Franja("15:00", "23:00"));

        resultado.Should().BeNull();
    }

    [Fact]
    public void Evaluar_EntraConUnaHora_CuandoLaFranjaLaContiene()
    {
        Evaluar("06:00", Franja("06:00", "12:00")).Should().NotBeNull();
        Evaluar("06:00", Franja("05:00", "08:00")).Should().NotBeNull();
    }

    [Fact]
    public void Evaluar_EntraConUnaHora_CuandoLaFranjaCruzaLaMedianocheYLaContieneAlDiaSiguiente()
    {
        Evaluar("06:00", Franja("22:00", "07:00", diaOffsetFin: 1)).Should().NotBeNull();
    }

    [Fact]
    public void Evaluar_NoEntraConUnaHora_CuandoNingunaFranjaLaContiene()
    {
        Evaluar("06:00", Franja("07:00", "15:00")).Should().BeNull();
    }

    [Fact]
    public void Evaluar_PonePrimeroLaFranjaQueEmpiezaALaHora_CuandoSePideUnaHora()
    {
        var exacta = Evaluar("06:00", Franja("06:00", "12:00"));
        var contiene = Evaluar("06:00", Franja("05:00", "08:00"));

        exacta!.Tipo.Should().Be(TipoCoincidenciaDeHorario.Exacto);
        exacta.DistanciaEnMinutos.Should().Be(0);
        contiene!.DistanciaEnMinutos.Should().Be(60);
    }

    [Fact]
    public void Evaluar_EsExacto_CuandoElRangoCruzaLaMedianocheYCoincideConLaFranja()
    {
        var resultado = Evaluar("22:00-06:00", Franja("22:00", "06:00", diaOffsetFin: 1));

        resultado!.Tipo.Should().Be(TipoCoincidenciaDeHorario.Exacto);
        resultado.DistanciaEnMinutos.Should().Be(0);
    }

    [Fact]
    public void Evaluar_Entra_CuandoElRangoCruzaLaMedianocheYLaFranjaEsDeMadrugada()
    {
        var resultado = Evaluar("22:00-06:00", Franja("00:00", "06:00"));

        resultado.Should().NotBeNull();
        resultado!.DistanciaEnMinutos.Should().Be(120);
    }

    [Fact]
    public void Evaluar_ElijeLaFranjaMasCercana_CuandoElTurnoTieneVarias()
    {
        var resultado = Evaluar("06:00-14:00", Franja("15:00", "23:00"), Franja("06:00", "14:00"));

        resultado!.Tipo.Should().Be(TipoCoincidenciaDeHorario.Exacto);
    }

    [Fact]
    public void TryParsear_RechazaElValor_CuandoEstaMalFormado()
    {
        PuntajeDeHorario.TryParsear("6am", out _).Should().BeFalse();
        PuntajeDeHorario.TryParsear("25:00", out _).Should().BeFalse();
        PuntajeDeHorario.TryParsear("06:00-", out _).Should().BeFalse();
        PuntajeDeHorario.TryParsear(null, out _).Should().BeFalse();
    }
}
