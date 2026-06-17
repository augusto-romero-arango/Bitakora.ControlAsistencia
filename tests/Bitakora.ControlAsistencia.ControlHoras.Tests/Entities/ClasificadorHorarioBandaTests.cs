// Issue #134: Clasificar segmentos horarios por banda y tipo de dia
// Tests directos sobre ClasificadorHorario.ClasificarBanda - sin harness de event sourcing.
// Precondicion de todos los tests: el intervalo es homogeneo (no cruza fronteras de banda).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

/// <summary>
/// Tests de ClasificadorHorario.ClasificarBanda.
/// Cubre CA-1 a CA-4: banda diurna y nocturna, incluyendo frontera exacta de las 19:00.
/// Anade los limites exactos de la banda diurna [06:00, 19:00): 06:00 inclusivo y 19:00 exclusivo.
/// </summary>
public class ClasificadorHorarioBandaTests
{
    // Helper: crea un IntervaloTemporal homogeneo en el mismo DiaOffset
    private static IntervaloTemporal Intervalo(int horaInicio, int minInicio, int horaFin, int minFin, int diaOffset = 0) =>
        IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(horaInicio, minInicio), diaOffset),
            new MomentoDelDia(new TimeOnly(horaFin, minFin), diaOffset));

    [Fact]
    public void ClasificarBanda_RetornaDiurna_CuandoIntervaloEntre08hY12h()
    {
        // CA-1: 08:00-12:00 esta completamente dentro de la banda diurna [06:00, 19:00)
        var intervalo = Intervalo(8, 0, 12, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Diurna);
    }

    [Fact]
    public void ClasificarBanda_RetornaNocturna_CuandoIntervaloEntre20hY23h()
    {
        // CA-2: 20:00-23:00 esta fuera de la banda diurna (>= 19:00) -> Nocturna
        var intervalo = Intervalo(20, 0, 23, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Nocturna);
    }

    [Fact]
    public void ClasificarBanda_RetornaNocturna_CuandoIntervaloEsMadrugadaEntre00h30Y05h30()
    {
        // CA-3: 00:30-05:30 madrugada del dia siguiente (DiaOffset=1) -> Nocturna
        // La hora 00:30 esta fuera del rango diurno [06:00, 19:00)
        var intervalo = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(0, 30), 1),
            new MomentoDelDia(new TimeOnly(5, 30), 1));

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Nocturna);
    }

    [Fact]
    public void ClasificarBanda_RetornaDiurna_CuandoIntervaloEntre18hY19h()
    {
        // CA-4: 18:00-19:00 -> Diurna
        // La frontera 19:00 es el extremo derecho exclusivo de la banda diurna;
        // todo lo anterior a 19:00 es diurno.
        var intervalo = Intervalo(18, 0, 19, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Diurna);
    }

    [Fact]
    public void ClasificarBanda_RetornaDiurna_CuandoIntervaloIniciaExactamenteEn06h()
    {
        // Limite inferior inclusivo de la banda diurna: 06:00 pertenece a [06:00, 19:00) -> Diurna.
        // Pinea el operador >= InicioDiurna contra una regresion a >.
        var intervalo = Intervalo(6, 0, 10, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Diurna);
    }

    [Fact]
    public void ClasificarBanda_RetornaNocturna_CuandoIntervaloIniciaExactamenteEn19h()
    {
        // Limite superior exclusivo de la banda diurna: 19:00 ya NO pertenece a [06:00, 19:00) -> Nocturna.
        // Complementa CA-4 (que termina en 19:00) verificando un segmento que inicia en 19:00.
        // Pinea el operador < InicioNocturna contra una regresion a <=.
        var intervalo = Intervalo(19, 0, 22, 0);

        var resultado = ClasificadorHorario.ClasificarBanda(intervalo);

        resultado.Should().Be(BandaHoraria.Nocturna);
    }
}
