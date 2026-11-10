// Referencia legal: Ley 51 de 1983 art. 1, art. 177 CST.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class CalendarioFestivosColombiaTests
{
    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsPrimeroDeEnero()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 1, 1)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsPrimeroDeMayo()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 5, 1)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsVeinteDeJulio()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 7, 20)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsSieteDeAgosto()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 8, 7)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsOchoDeDiciembre()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 12, 8)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsVeinticincoDeDiciembre()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 12, 25)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsJuevesSanto2026()
    {
        // Pascua 2026 = 5 abr; Jueves Santo = Pascua - 3 dias = 2 abr
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 4, 2)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsViernesSanto2026()
    {
        // Pascua 2026 = 5 abr; Viernes Santo = Pascua - 2 dias = 3 abr
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 4, 3)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsJuevesSanto2027()
    {
        // Pascua 2027 = 28 mar; Jueves Santo = Pascua - 3 dias = 25 mar
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2027, 3, 25)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsViernesSanto2027()
    {
        // Pascua 2027 = 28 mar; Viernes Santo = Pascua - 2 dias = 26 mar
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2027, 3, 26)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalSeisDeEnero2026EsMartes()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 1, 6)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoReyesMagos2026()
    {
        // 6 ene 2026 (martes) -> lunes siguiente = 12 ene 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 1, 12)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalDiecinueveMarzo2026EsJueves()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 3, 19)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoSanJose2026()
    {
        // 19 mar 2026 (jueves) -> lunes siguiente = 23 mar 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 3, 23)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalQuinceAgosto2026EsSabado()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 8, 15)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoAsuncion2026()
    {
        // 15 ago 2026 (sabado) -> lunes siguiente = 17 ago 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 8, 17)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalPrimeroNoviembre2026EsDomingo()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 11, 1)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoTodosSantos2026()
    {
        // 1 nov 2026 (domingo) -> lunes siguiente = 2 nov 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 11, 2)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoSanPedroYSanPablo2026CaeLunesYPermanece()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 29)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoDiaRaza2026CaeLunesYPermanece()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 10, 12)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsAscension2026()
    {
        // Pascua 2026 (5 abr) + 39 = 14 may (jue) -> lunes 18 may 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 5, 18)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaBaseAscension2026NoEsFestivo()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 5, 14)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsCorpusChristi2026()
    {
        // Pascua 2026 (5 abr) + 60 = 4 jun (jue) -> lunes 8 jun 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 8)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaBaseCorpusChristi2026NoEsFestivo()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 4)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsSagradoCorazon2026()
    {
        // Pascua 2026 (5 abr) + 68 = 12 jun (vie) -> lunes 15 jun 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 15)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaBaseSagradoCorazon2026NoEsFestivo()
    {
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 12)).Should().BeFalse();
    }

    [Fact]
    public void ObtenerFestivos_RetornaDieciochoFechas_CuandoAnoEs2026()
    {
        // 8 fijos + 10 trasladables
        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2026);

        festivos.Count.Should().Be(18);
    }

    [Fact]
    public void ObtenerFestivos_RetornaDieciochoFechas_CuandoAnoEs2027()
    {
        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2027);

        festivos.Count.Should().Be(18);
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaEsDiaHabilSinFestivo()
    {
        // Jueves 5 mar 2026; el primer festivo despues de enero es el 23 mar.
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 3, 5)).Should().BeFalse();
    }

    [Fact]
    public void ObtenerFestivos_EstaOrdenado_CuandoAnoEs2026()
    {
        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2026);

        festivos.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ObtenerFestivos_RetornaListaCompletaVerificada_CuandoAnoEs2026()
    {
        // Lista verificada manualmente contra el calendario oficial colombiano 2026.
        var esperados = new[]
        {
            new DateOnly(2026, 1,  1),   // Año Nuevo
            new DateOnly(2026, 1, 12),   // Reyes Magos (6 ene mar -> lun 12 ene)
            new DateOnly(2026, 3, 23),   // San Jose (19 mar jue -> lun 23 mar)
            new DateOnly(2026, 4,  2),   // Jueves Santo
            new DateOnly(2026, 4,  3),   // Viernes Santo
            new DateOnly(2026, 5,  1),   // Dia del Trabajo
            new DateOnly(2026, 5, 18),   // Ascension (14 may jue -> lun 18 may)
            new DateOnly(2026, 6,  8),   // Corpus Christi (4 jun jue -> lun 8 jun)
            new DateOnly(2026, 6, 15),   // Sagrado Corazon (12 jun vie -> lun 15 jun)
            new DateOnly(2026, 6, 29),   // San Pedro y San Pablo (lun, permanece)
            new DateOnly(2026, 7, 20),   // Independencia de Colombia
            new DateOnly(2026, 8,  7),   // Batalla de Boyaca
            new DateOnly(2026, 8, 17),   // Asuncion de la Virgen (15 ago sab -> lun 17 ago)
            new DateOnly(2026, 10, 12),  // Dia de la Raza (lun, permanece)
            new DateOnly(2026, 11,  2),  // Todos los Santos (1 nov dom -> lun 2 nov)
            new DateOnly(2026, 11, 16),  // Independencia de Cartagena (11 nov mie -> lun 16 nov)
            new DateOnly(2026, 12,  8),  // Inmaculada Concepcion
            new DateOnly(2026, 12, 25),  // Navidad
        };

        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2026);

        festivos.Should().BeEquivalentTo(esperados, options => options.WithStrictOrdering());
    }
}
