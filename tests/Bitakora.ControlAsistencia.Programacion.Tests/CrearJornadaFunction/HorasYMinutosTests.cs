using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

public class HorasYMinutosTests
{
    [Fact]
    public void Crear_ConservaNotacionHumana_CuandoHorasYMinutosSonValidos()
    {
        HorasYMinutos.Crear(8, 12).ToString().Should().Be("8 h 12 min");
        HorasYMinutos.Crear(42, 0).ToString().Should().Be("42 h");
        HorasYMinutos.Crear(0, 45).ToString().Should().Be("45 min");
        HorasYMinutos.Crear(0, 0).ToString().Should().Be("0 h");
    }

    [Fact]
    public void Crear_RechazaHorasNegativas_ConMensaje()
    {
        var act = () => HorasYMinutos.Crear(-1, 0);
        var errores = act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions;
        errores.Select(e => e.Message).Should().Contain(HorasYMinutos.Mensajes.HorasNegativas);
    }

    [Fact]
    public void Crear_RechazaMinutosFueraDeRango_ConMensaje()
    {
        foreach (var minutos in new[] { -1, 60, 90 })
        {
            var act = () => HorasYMinutos.Crear(0, minutos);
            var errores = act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions;
            errores.Select(e => e.Message).Should().Contain(HorasYMinutos.Mensajes.MinutosFueraDeRango);
        }
    }

    [Fact]
    public void Crear_AcumulaDosErrores_CuandoAmbasPartesSonInvalidas()
    {
        var act = () => HorasYMinutos.Crear(-1, 60);
        var mensajes = act.Should().ThrowExactly<AggregateException>().Which.InnerExceptions.Select(e => e.Message);
        mensajes.Should().BeEquivalentTo(new[]
        {
            HorasYMinutos.Mensajes.HorasNegativas, HorasYMinutos.Mensajes.MinutosFueraDeRango
        });
    }

    [Fact]
    public void CompareTo_OrdenaPorDuracionTotal_CuandoLasHorasSonDistintas()
    {
        HorasYMinutos.Crear(8, 0).CompareTo(HorasYMinutos.Crear(7, 59)).Should().BePositive();
        HorasYMinutos.Crear(0, 45).CompareTo(HorasYMinutos.Crear(1, 0)).Should().BeNegative();
        HorasYMinutos.Crear(8, 12).CompareTo(HorasYMinutos.Crear(8, 12)).Should().Be(0);
    }

    [Fact]
    public void Por_MultiplicaDuracion_CuandoSonSieteDias()
    {
        HorasYMinutos.Crear(8, 0).Por(7).ToString().Should().Be("56 h");
        HorasYMinutos.Crear(0, 45).Por(2).ToString().Should().Be("1 h 30 min");
    }

    [Fact]
    public void Por_RechazaFactorNegativo_CuandoRomperiaLaInvariante()
    {
        var act = () => HorasYMinutos.Crear(8, 0).Por(-1);
        act.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }
}
