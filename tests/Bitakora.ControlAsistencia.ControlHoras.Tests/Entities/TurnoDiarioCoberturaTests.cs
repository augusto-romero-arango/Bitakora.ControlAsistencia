using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class TurnoDiarioCoberturaTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);

    private static DateTime En(int dia, int h, int m) => new(2026, 3, dia, h, m, 0);

    private static FranjaProgramada Franja(int hi, int mi, int hf, int mf, int offset = 0) =>
        new(new TimeOnly(hi, mi), new TimeOnly(hf, mf), offset, [], [], "");

    [Fact]
    public void Cobertura_VaDesdeDosHorasAntesHastaCuatroDespues_CuandoTurnoFijo()
    {
        var turno = new TurnoDiario("Fijo", [Franja(7, 0, 16, 0)], "");

        var cobertura = turno.Cobertura(Fecha);

        cobertura.Should().Be(new CoberturaDeMarcacion(En(15, 5, 0), En(15, 20, 0)));
    }

    [Fact]
    public void Cobertura_ResuelveFinEnElDiaSiguiente_CuandoFranjaTieneDiaOffsetFin()
    {
        var turno = new TurnoDiario("Nocturno", [Franja(17, 0, 1, 0, 1)], "");

        var cobertura = turno.Cobertura(Fecha);

        cobertura.Should().Be(new CoberturaDeMarcacion(En(15, 15, 0), En(16, 5, 0)));
    }

    [Fact]
    public void Cobertura_UsaMenorInicioYMayorFin_CuandoTurnoPartido()
    {
        var turno = new TurnoDiario("Partido", [Franja(14, 0, 18, 0), Franja(6, 0, 12, 0)], "");

        var cobertura = turno.Cobertura(Fecha);

        cobertura.Should().Be(new CoberturaDeMarcacion(En(15, 4, 0), En(15, 22, 0)));
    }

    [Fact]
    public void Cobertura_RetornaNull_CuandoTurnoNoTieneFranjas()
    {
        var turno = new TurnoDiario("Vacio", [], "");

        var cobertura = turno.Cobertura(Fecha);

        cobertura.Should().BeNull();
    }

    [Fact]
    public void Contiene_EsSemiabierto_CuandoInstanteEnLosLimites()
    {
        var cobertura = new TurnoDiario("Fijo", [Franja(7, 0, 16, 0)], "").Cobertura(Fecha)!;

        var resultado = new[]
        {
            cobertura.Contiene(En(15, 4, 59)),
            cobertura.Contiene(En(15, 5, 0)),
            cobertura.Contiene(En(15, 19, 59)),
            cobertura.Contiene(En(15, 20, 0))
        };

        resultado.Should().Equal(false, true, true, false);
    }
}
