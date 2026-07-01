// Issue #134: Clasificar segmentos horarios por banda y tipo de dia
// Tests directos sobre ClasificadorHorario.ClasificarTipoDia - sin harness de event sourcing.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

/// <summary>
/// Tests de ClasificadorHorario.ClasificarTipoDia.
/// Cubre CA-5 a CA-10: dias habiles, domingos, festivos, y semantica del DiaOffset.
/// Fechas ancla fijas segun issue:
///   2026-03-16 = lunes habil
///   2026-03-15 = domingo
///   2026-03-14 = sabado
///   2026-03-13 = viernes habil
/// </summary>
public class ClasificadorHorarioTipoDiaTests
{
    // Fechas ancla fijas segun el issue
    private static readonly DateOnly FechaAnclaLunes = new(2026, 3, 16);    // lunes habil
    private static readonly DateOnly FechaAnclaSabado = new(2026, 3, 14);   // sabado
    private static readonly DateOnly FechaAnclaDomingo = new(2026, 3, 15);  // domingo

    // Momento de referencia para los tests que solo verifican tipo de dia (no offset)
    private static readonly MomentoDelDia Momento08h = new(new TimeOnly(8, 0));

    [Fact]
    public void ClasificarTipoDia_RetornaHabil_CuandoFechaEsLunesNoFestivo()
    {
        // CA-5: lunes 2026-03-16, esFestivo siempre false -> Habil
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaLunes, _ => false);

        resultado.Should().Be(TipoDia.Habil);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaHabil_CuandoFechaEsSabadoNoFestivo()
    {
        // CA-6: sabado 2026-03-14, esFestivo siempre false -> Habil
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaSabado, _ => false);

        resultado.Should().Be(TipoDia.Habil);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFechaEsDomingoNoFestivo()
    {
        // CA-7: domingo 2026-03-15, esFestivo siempre false -> DominicalFestivo
        // DayOfWeek == Sunday es suficiente para clasificar como DominicalFestivo
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaDomingo, _ => false);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFechaEsLunesFestivo()
    {
        // CA-8: lunes 2026-03-16, esFestivo siempre true -> DominicalFestivo
        // esFestivo(fechaResuelta) = true es suficiente para clasificar como DominicalFestivo
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaLunes, _ => true);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFechaEsDomingoYFestivo()
    {
        // CA-9: domingo 2026-03-15 que tambien es festivo -> DominicalFestivo (sin duplicar recargo)
        // La regla es OR inclusivo: domingo || festivo. No se suma doble recargo.
        var resultado = ClasificadorHorario.ClasificarTipoDia(Momento08h, FechaAnclaDomingo, _ => true);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoDiaOffsetApuntaAlDomingoSiguiente()
    {
        // CA-10: MomentoDelDia(02:00, DiaOffset=1) + fechaAncla sabado 2026-03-14
        // Fecha resuelta = 2026-03-14.AddDays(1) = 2026-03-15 (domingo) -> DominicalFestivo
        // Este test verifica que la evaluacion ocurre en la fecha RESUELTA, no en la fecha ancla.
        var momentoMadrugada = new MomentoDelDia(new TimeOnly(2, 0), 1);

        var resultado = ClasificadorHorario.ClasificarTipoDia(momentoMadrugada, FechaAnclaSabado, _ => false);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }

    [Fact]
    public void ClasificarTipoDia_RetornaDominicalFestivo_CuandoFestivoCaeEnLaFechaResueltaPorOffset()
    {
        // Refuerza la semantica del offset sobre el eje FESTIVO (distinto de CA-10, que usa domingo):
        // esFestivo se consulta con la fecha RESUELTA (ancla + offset), no con el ancla.
        // Ancla viernes 2026-03-13 (habil, no festivo), offset=1 -> sabado 2026-03-14 (no domingo).
        // El stub marca festivo SOLO el sabado resuelto; si la implementacion consultara el ancla
        // viernes el resultado seria Habil y este test fallaria.
        var anclaViernes = new DateOnly(2026, 3, 13);
        var sabadoResuelto = new DateOnly(2026, 3, 14);
        var momentoMadrugada = new MomentoDelDia(new TimeOnly(2, 0), 1);

        var resultado = ClasificadorHorario.ClasificarTipoDia(
            momentoMadrugada, anclaViernes, fecha => fecha == sabadoResuelto);

        resultado.Should().Be(TipoDia.DominicalFestivo);
    }
}
