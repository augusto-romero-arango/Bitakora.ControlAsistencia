using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerCuadroDeTurnos;

public class CuadroDeTurnosRespuestaTests
{
    private static CuadroDeTurnos Vista(
        IReadOnlyList<DiaDelCuadroDeTurnos>? dias = null,
        bool completa = false,
        Guid? jornadaId = null,
        LimitesDelCuadro? limites = null,
        IReadOnlyList<AdvertenciaDelCuadroDeTurnos>? advertencias = null) =>
        new("plantilla-001", "Ciclo Cocina", 3, dias ?? [], completa, jornadaId, limites, advertencias ?? []);

    [Fact]
    public void Componer_CopiaLaVistaTalCual()
    {
        var dias = new[]
        {
            new DiaDelCuadroDeTurnos(1, "turno-1", "Turno Manana", "Turno Manana 06:00-14:00", true, false),
            new DiaDelCuadroDeTurnos(3, "turno-2", "Turno Viejo", "Turno Viejo", false, true),
        };

        var respuesta = CuadroDeTurnosRespuesta.Componer(Vista(dias, completa: true));

        respuesta.Id.Should().Be("plantilla-001");
        respuesta.Nombre.Should().Be("Ciclo Cocina");
        respuesta.DiasDelCiclo.Should().Be(3);
        respuesta.Completa.Should().BeTrue();
        respuesta.Dias.Should().Equal(
            new DiaDelCuadroDeTurnosRespuesta(1,
                new TurnoDelCuadroDeTurnosRespuesta("turno-1", "Turno Manana", "Turno Manana 06:00-14:00", true, false)),
            new DiaDelCuadroDeTurnosRespuesta(3,
                new TurnoDelCuadroDeTurnosRespuesta("turno-2", "Turno Viejo", "Turno Viejo", false, true)));
    }

    [Fact]
    public void Componer_CopiaDescansoYHorarioAbierto_DeCadaDia()
    {
        var dias = new[]
        {
            new DiaDelCuadroDeTurnos(1, "t-1", "Descanso", "(descanso)", true, false, EsDescanso: true),
            new DiaDelCuadroDeTurnos(2, "t-2", "Abierto", "04:00-06:00", true, false,
                HorarioAbierto: new HorarioAbiertoDelCuadro("2 h")),
        };

        var respuesta = CuadroDeTurnosRespuesta.Componer(Vista(dias));

        respuesta.Dias[0].Turno.EsDescanso.Should().BeTrue();
        respuesta.Dias[0].Turno.HorarioAbierto.Should().BeNull();
        respuesta.Dias[1].Turno.EsDescanso.Should().BeFalse();
        respuesta.Dias[1].Turno.HorarioAbierto.Should().Be(new HorarioAbiertoDelCuadroDeTurnosRespuesta("2 h"));
    }

    [Fact]
    public void Componer_LlevaFalsoYNulo_CuandoElDiaTieneValoresPorDefecto()
    {
        var dias = new[] { new DiaDelCuadroDeTurnos(1, "t-1", "Turno", "06:00-14:00", true, false) };

        var turno = CuadroDeTurnosRespuesta.Componer(Vista(dias)).Dias.Single().Turno;

        turno.EsDescanso.Should().BeFalse();
        turno.HorarioAbierto.Should().BeNull();
    }

    [Fact]
    public void Componer_DejaJornadaEnNull_CuandoLaVistaNoTieneLimites()
    {
        CuadroDeTurnosRespuesta.Componer(Vista()).Jornada.Should().BeNull();
    }

    [Fact]
    public void Componer_ArmaLaJornadaConTiemposComoHorasYMinutos_CuandoLaVistaTieneLimites()
    {
        var jornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000a1");
        var limites = new LimitesDelCuadro(2670, 600, 135, 1, "44:30 semanales");

        var respuesta = CuadroDeTurnosRespuesta.Componer(Vista(jornadaId: jornadaId, limites: limites));

        respuesta.Jornada.Should().Be(new JornadaDelCuadroDeTurnosRespuesta(
            jornadaId,
            new HorasYMinutosRespuesta(44, 30),
            new HorasYMinutosRespuesta(10, 0),
            new HorasYMinutosRespuesta(2, 15),
            1,
            "44:30 semanales"));
    }

    [Fact]
    public void Componer_DejaAdvertenciasVacias_CuandoLaVistaNoTiene()
    {
        CuadroDeTurnosRespuesta.Componer(Vista()).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Componer_ExpresaLaMagnitudComoHorasYMinutosConSuDia_CuandoLaAdvertenciaEsDiaria()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadroDeTurnos("SuperaTopeDiario", 2, 90)]);

        var advertencia = CuadroDeTurnosRespuesta.Componer(vista).Advertencias.Single();

        advertencia.Tipo.Should().Be("SuperaTopeDiario");
        advertencia.Dia.Should().Be(2);
        advertencia.Magnitud.Should().Be(new HorasYMinutosRespuesta(1, 30));
    }

    [Fact]
    public void Componer_DejaElDiaEnNull_CuandoLaAdvertenciaEsPlantillaSinJornada()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadroDeTurnos("PlantillaSinJornada", null, 0)]);

        var advertencia = CuadroDeTurnosRespuesta.Componer(vista).Advertencias.Single();

        advertencia.Dia.Should().BeNull();
        advertencia.Magnitud.Should().Be(new HorasYMinutosRespuesta(0, 0));
    }

    [Fact]
    public void Componer_DescribeLaAdvertenciaSinDia_CuandoEsPlantillaSinJornada()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadroDeTurnos("PlantillaSinJornada", null, 0)]);

        CuadroDeTurnosRespuesta.Componer(vista).Advertencias.Single().Descripcion
            .Should().Be("La plantilla no tiene Jornada asignada contra la cual auditarse");
    }

    [Fact]
    public void Componer_DescribeLaAdvertenciaConSuDia_CuandoEsDiaSinTurno()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadroDeTurnos("DiaSinTurno", 3, 0)]);

        CuadroDeTurnosRespuesta.Componer(vista).Advertencias.Single().Descripcion
            .Should().Be("Dia 3: no tiene turno asignado");
    }

    [Fact]
    public void Componer_DescribeLaAdvertenciaConSuDiaYMagnitudEnHorasYMinutos_CuandoSuperaTopeDiario()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadroDeTurnos("SuperaTopeDiario", 2, 90)]);

        CuadroDeTurnosRespuesta.Componer(vista).Advertencias.Single().Descripcion
            .Should().Be("Dia 2: supera el tope diario por 1:30 h:min");
    }

    [Fact]
    public void Componer_DescribeLaAdvertenciaConSuDiaYMagnitudEnHorasYMinutos_CuandoQuedaPorDebajoDelMinimoDiario()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadroDeTurnos("PorDebajoDelMinimoDiario", 1, 45)]);

        CuadroDeTurnosRespuesta.Componer(vista).Advertencias.Single().Descripcion
            .Should().Be("Dia 1: queda por debajo del minimo diario por 0:45 h:min");
    }
}
