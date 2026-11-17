using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerCuadroDeTurnos;

public class CuadroDeTurnosRespuestaTests
{
    private static CuadroDeTurnos Vista(
        IReadOnlyList<DiaDelCuadroDeTurnos>? dias = null,
        bool completa = false,
        Guid? jornadaId = null) =>
        new("plantilla-001", "Ciclo Cocina", 3, dias ?? [], completa, jornadaId);

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
    public void Componer_DejaJornadaIdEnNull_CuandoLaVistaNoTieneJornada()
    {
        CuadroDeTurnosRespuesta.Componer(Vista()).JornadaId.Should().BeNull();
    }

    [Fact]
    public void Componer_CopiaElJornadaId_CuandoLaVistaTieneJornada()
    {
        var jornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000a1");

        CuadroDeTurnosRespuesta.Componer(Vista(jornadaId: jornadaId)).JornadaId.Should().Be(jornadaId);
    }
}
