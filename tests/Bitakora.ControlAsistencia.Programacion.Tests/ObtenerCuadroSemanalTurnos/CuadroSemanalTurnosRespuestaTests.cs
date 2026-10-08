// Mapeo PURO vista -> respuesta (sin QuerySession ni FichaTurno). Oraculo armado a mano
// (MEF-ADR-0002). Las descripciones de advertencias salen de .resx (MEF-ADR-0009): se verifica
// que existan, no su redaccion.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerCuadroSemanalTurnos;

public class CuadroSemanalTurnosRespuestaTests
{
    private static CuadroSemanalTurnos Vista(
        IReadOnlyList<DiaDelCuadro>? dias = null,
        bool completa = false,
        Guid? jornadaId = null,
        LimitesDelCuadro? limites = null,
        IReadOnlyList<AdvertenciaDelCuadro>? advertencias = null) =>
        new("plantilla-001", "Semana Cocina", 2, dias ?? [], completa, jornadaId, limites, advertencias ?? []);

    [Fact]
    public void Componer_CopiaLaVistaTalCual_SinConsultarFichas()
    {
        var dias = new[]
        {
            new DiaDelCuadro(1, 1, "turno-1", "Turno Manana", "Turno Manana 06:00-14:00", true, false),
            new DiaDelCuadro(1, 2, "turno-2", "Turno Viejo", "Turno Viejo", false, true),
        };

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(Vista(dias, completa: true));

        respuesta.Id.Should().Be("plantilla-001");
        respuesta.Nombre.Should().Be("Semana Cocina");
        respuesta.Semanas.Should().Be(2);
        respuesta.Completa.Should().BeTrue();
        respuesta.Dias.Should().Equal(
            new DiaDelCuadroRespuesta(1, 1,
                new TurnoDelCuadroRespuesta("turno-1", "Turno Manana", "Turno Manana 06:00-14:00", true, false)),
            new DiaDelCuadroRespuesta(1, 2,
                new TurnoDelCuadroRespuesta("turno-2", "Turno Viejo", "Turno Viejo", false, true)));
    }

    [Fact]
    public void Componer_DejaJornadaEnNull_CuandoLaVistaNoTieneLimites()
    {
        CuadroSemanalTurnosRespuesta.Componer(Vista()).Jornada.Should().BeNull();
    }

    [Fact]
    public void Componer_ArmaLaJornadaConTiemposComoHorasYMinutos_CuandoLaVistaTieneLimites()
    {
        var jornadaId = Guid.Parse("019600b0-0000-7000-8000-0000000000a1");
        var limites = new LimitesDelCuadro(2670, 600, 135, 1, "44:30 semanales");

        var respuesta = CuadroSemanalTurnosRespuesta.Componer(Vista(jornadaId: jornadaId, limites: limites));

        respuesta.Jornada.Should().Be(new JornadaDelCuadroRespuesta(
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
        CuadroSemanalTurnosRespuesta.Componer(Vista()).Advertencias.Should().BeEmpty();
    }

    [Fact]
    public void Componer_ExpresaLaMagnitudComoHorasYMinutos_CuandoLaAdvertenciaEsDeHoras()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadro("SuperaTopeDiario", 1, 2, 90)]);

        var advertencia = CuadroSemanalTurnosRespuesta.Componer(vista).Advertencias.Single();

        advertencia.Tipo.Should().Be("SuperaTopeDiario");
        advertencia.Semana.Should().Be(1);
        advertencia.Dia.Should().Be(2);
        advertencia.Magnitud.Should().Be(new HorasYMinutosRespuesta(1, 30));
    }

    [Fact]
    public void Componer_ExpresaLaMagnitudComoEntero_CuandoLaAdvertenciaEsDeDias()
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadro("FaltanDiasDeDescanso", 2, null, 3)]);

        var advertencia = CuadroSemanalTurnosRespuesta.Componer(vista).Advertencias.Single();

        advertencia.Dia.Should().BeNull();
        advertencia.Magnitud.Should().Be(3);
    }

    [Theory]
    [InlineData("PlantillaSinJornada")]
    [InlineData("DiaSinTurno")]
    [InlineData("SuperaTopeDiario")]
    [InlineData("PorDebajoDelMinimoDiario")]
    [InlineData("SuperaHorasSemanales")]
    [InlineData("PorDebajoDeHorasSemanales")]
    [InlineData("FaltanDiasDeDescanso")]
    [InlineData("SobranDiasDeDescanso")]
    public void Componer_DescribeLaAdvertenciaDesdeResx_CuandoCadaTipo(string tipo)
    {
        var vista = Vista(advertencias: [new AdvertenciaDelCuadro(tipo, 1, 1, 60)]);

        var advertencia = CuadroSemanalTurnosRespuesta.Componer(vista).Advertencias.Single();

        advertencia.Descripcion.Should().NotBeNullOrWhiteSpace();
    }
}
