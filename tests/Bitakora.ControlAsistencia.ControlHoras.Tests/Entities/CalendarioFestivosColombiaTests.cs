// Issue #113: Calcular festivos colombianos por año
// Referencia legal: Ley 51 de 1983 art. 1, art. 177 CST
// Algoritmo de Computus: Meeus/Jones/Butcher (Jean Meeus, Astronomical Algorithms, cap. 9)

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

/// <summary>
/// Tests de CalendarioFestivosColombia - funcion estatica pura sin event sourcing.
/// Interfaz publica: ObtenerFestivos(int año), EsFestivo(DateOnly fecha).
/// Privados (no testeables directamente): CalcularPascua, TrasladarAlLunes.
/// </summary>
public class CalendarioFestivosColombiaTests
{
    // ---- CA-1 a CA-6: Festivos fijos (no trasladables, siempre la misma fecha) ----

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsPrimeroDeEnero()
    {
        // CA-1: 1 ene siempre es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 1, 1)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsPrimeroDeMayo()
    {
        // CA-2: 1 may siempre es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 5, 1)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsVeinteDeJulio()
    {
        // CA-3: 20 jul siempre es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 7, 20)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsSieteDeAgosto()
    {
        // CA-4: 7 ago siempre es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 8, 7)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsOchoDeDeciembre()
    {
        // CA-5: 8 dic siempre es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 12, 8)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsVeinticincoDeDeciembre()
    {
        // CA-6: 25 dic siempre es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 12, 25)).Should().BeTrue();
    }

    // ---- CA-7: Jueves Santo y Viernes Santo 2026 (Pascua = 5 abr 2026) ----

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

    // ---- CA-8: Jueves Santo y Viernes Santo 2027 (Pascua = 28 mar 2027) ----

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

    // ---- CA-9 + CA-11: Trasladable que cae martes - Reyes Magos 2026 ----

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalSeisDeEnero2026EsMartes()
    {
        // CA-9: 6 ene 2026 cae martes - la fecha original NO es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 1, 6)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoReyesMagos2026()
    {
        // CA-9: 6 ene 2026 (martes) -> lunes siguiente = 12 ene 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 1, 12)).Should().BeTrue();
    }

    // ---- CA-11: Trasladable que cae jueves - San Jose 2026 ----

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalDiecinueveMarzo2026EsJueves()
    {
        // CA-11: 19 mar 2026 cae jueves - la fecha original NO es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 3, 19)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoSanJose2026()
    {
        // CA-11: 19 mar 2026 (jueves) -> lunes siguiente = 23 mar 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 3, 23)).Should().BeTrue();
    }

    // ---- CA-11: Trasladable que cae sabado - Asuncion de la Virgen 2026 ----

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalQuinceAgosto2026EsSabado()
    {
        // CA-11: 15 ago 2026 cae sabado - la fecha original NO es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 8, 15)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoAsuncion2026()
    {
        // CA-11: 15 ago 2026 (sabado) -> lunes siguiente = 17 ago 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 8, 17)).Should().BeTrue();
    }

    // ---- CA-10: Trasladable que cae domingo - Todos los Santos 2026 ----

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaOriginalPrimeroNoviembre2026EsDomingo()
    {
        // CA-10: 1 nov 2026 cae domingo - la fecha original NO es festivo
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 11, 1)).Should().BeFalse();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsLunesTrasladoTodosSantos2026()
    {
        // CA-10: 1 nov 2026 (domingo) -> lunes siguiente = 2 nov 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 11, 2)).Should().BeTrue();
    }

    // ---- CA-12: Trasladable que cae lunes permanece ese lunes ----

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoSanPedroYSanPablo2026CaeLunesYPermanece()
    {
        // CA-12: 29 jun 2026 es lunes (San Pedro y San Pablo) - permanece 29 jun
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 29)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoDiaRaza2026CaeLunesYPermanece()
    {
        // CA-12: 12 oct 2026 es lunes (Dia de la Raza) - permanece 12 oct
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 10, 12)).Should().BeTrue();
    }

    // ---- CA-13: Ascension del Senor 2026 (Pascua + 39 dias) ----

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsAscension2026()
    {
        // CA-13: Pascua 2026 (5 abr) + 39 = 14 may (jue) -> lunes 18 may 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 5, 18)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaBaseAscension2026NoEsFestivo()
    {
        // CA-13: 14 may 2026 (jueves) es la fecha base de Ascension, se traslada a 18 may
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 5, 14)).Should().BeFalse();
    }

    // ---- CA-14: Corpus Christi 2026 (Pascua + 60 dias) ----

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsCorpusChristi2026()
    {
        // CA-14: Pascua 2026 (5 abr) + 60 = 4 jun (jue) -> lunes 8 jun 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 8)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaBaseCorpusChristi2026NoEsFestivo()
    {
        // CA-14: 4 jun 2026 (jueves) es la fecha base de Corpus Christi, se traslada a 8 jun
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 4)).Should().BeFalse();
    }

    // ---- CA-15: Sagrado Corazon de Jesus 2026 (Pascua + 68 dias) ----

    [Fact]
    public void EsFestivo_RetornaTrue_CuandoFechaEsSagradoCorazon2026()
    {
        // CA-15: Pascua 2026 (5 abr) + 68 = 12 jun (vie) -> lunes 15 jun 2026
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 15)).Should().BeTrue();
    }

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaBaseSagradoCorazon2026NoEsFestivo()
    {
        // CA-15: 12 jun 2026 (viernes) es la fecha base de Sagrado Corazon, se traslada a 15 jun
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 6, 12)).Should().BeFalse();
    }

    // ---- CA-16: ObtenerFestivos retorna exactamente 18 fechas ----

    [Fact]
    public void ObtenerFestivos_RetornaDieciochoFechas_CuandoAnoEs2026()
    {
        // CA-16: los 18 festivos de Colombia (8 fijos + 10 trasladables)
        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2026);

        festivos.Count.Should().Be(18);
    }

    [Fact]
    public void ObtenerFestivos_RetornaDieciochoFechas_CuandoAnoEs2027()
    {
        // CA-16: la cuenta debe ser 18 para cualquier año
        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2027);

        festivos.Count.Should().Be(18);
    }

    // ---- CA-17: EsFestivo retorna true para festivo y false para dia habil ----

    [Fact]
    public void EsFestivo_RetornaFalse_CuandoFechaEsDiaHabilSinFestivo()
    {
        // CA-17: jueves 5 mar 2026 no es festivo (el primer festivo post-enero es 23 mar)
        CalendarioFestivosColombia.EsFestivo(new DateOnly(2026, 3, 5)).Should().BeFalse();
    }

    // ---- CA-18: Los festivos estan ordenados cronologicamente ----

    [Fact]
    public void ObtenerFestivos_EstaOrdenado_CuandoAnoEs2026()
    {
        // CA-18: la lista debe venir ordenada de menor a mayor
        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2026);

        festivos.Should().BeInAscendingOrder();
    }

    // ---- CA-19: Lista completa 2026 verificada contra calendario oficial ----

    [Fact]
    public void ObtenerFestivos_RetornaListaCompletaVerificada_CuandoAnoEs2026()
    {
        // CA-19: lista verificada manualmente contra calendario oficial colombiano 2026
        // Festivos fijos: 1 ene, 1 may, 20 jul, 7 ago, 8 dic, 25 dic
        // Jueves y Viernes Santo (Pascua 5 abr): 2 abr, 3 abr
        // Trasladables fijos: 6 ene(mar)→12 ene, 19 mar(jue)→23 mar, 29 jun(lun), 15 ago(sab)→17 ago,
        //                     12 oct(lun), 1 nov(dom)→2 nov, 11 nov(mie)→16 nov
        // Trasladables pascuales: Ascension(+39=14may jue)→18 may, Corpus(+60=4jun jue)→8 jun,
        //                         SagradoCorazon(+68=12jun vie)→15 jun
        var esperados = new[]
        {
            new DateOnly(2026, 1,  1),   // Año Nuevo
            new DateOnly(2026, 1, 12),   // Reyes Magos (6 ene mar → lun 12 ene)
            new DateOnly(2026, 3, 23),   // San Jose (19 mar jue → lun 23 mar)
            new DateOnly(2026, 4,  2),   // Jueves Santo
            new DateOnly(2026, 4,  3),   // Viernes Santo
            new DateOnly(2026, 5,  1),   // Dia del Trabajo
            new DateOnly(2026, 5, 18),   // Ascension (14 may jue → lun 18 may)
            new DateOnly(2026, 6,  8),   // Corpus Christi (4 jun jue → lun 8 jun)
            new DateOnly(2026, 6, 15),   // Sagrado Corazon (12 jun vie → lun 15 jun)
            new DateOnly(2026, 6, 29),   // San Pedro y San Pablo (lun, permanece)
            new DateOnly(2026, 7, 20),   // Independencia de Colombia
            new DateOnly(2026, 8,  7),   // Batalla de Boyaca
            new DateOnly(2026, 8, 17),   // Asuncion de la Virgen (15 ago sab → lun 17 ago)
            new DateOnly(2026, 10, 12),  // Dia de la Raza (lun, permanece)
            new DateOnly(2026, 11,  2),  // Todos los Santos (1 nov dom → lun 2 nov)
            new DateOnly(2026, 11, 16),  // Independencia de Cartagena (11 nov mie → lun 16 nov)
            new DateOnly(2026, 12,  8),  // Inmaculada Concepcion
            new DateOnly(2026, 12, 25),  // Navidad
        };

        var festivos = CalendarioFestivosColombia.ObtenerFestivos(2026);

        festivos.Should().BeEquivalentTo(esperados, options => options.WithStrictOrdering());
    }
}
