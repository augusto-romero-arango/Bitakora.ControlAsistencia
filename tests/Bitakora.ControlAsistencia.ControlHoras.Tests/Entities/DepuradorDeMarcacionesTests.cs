// Issue #122: Extraer depurador de marcaciones contra franjas del turno

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

/// <summary>
/// Tests de DepuradorDeMarcaciones - cubre el algoritmo secuencial por rango completo.
/// Un Fact por criterio de aceptacion (CA-1 a CA-8 y CA-10).
/// </summary>
public class DepuradorDeMarcacionesTests
{
    // Franjas nombradas semanticamente
    // Issue #288: Descripcion (dato derivado) es irrelevante para estos tests -> placeholder "".
    private static readonly FranjaProgramada Franja06_14 = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    private static readonly FranjaProgramada Franja06_12 = new(
        new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], [], "");

    private static readonly FranjaProgramada Franja14_18 = new(
        new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");

    // DiaOffsetFin=1 porque 22:00 > 06:00 (fin al dia siguiente)
    private static readonly FranjaProgramada Franja22_06_Nocturna = new(
        new TimeOnly(22, 0), new TimeOnly(6, 0), 1, [], [], "");

    // Fecha de referencia para todos los tests
    private static readonly DateOnly FechaBase = new(2026, 3, 15);

    // Helpers para construir timestamps con nombres semanticos
    private static DateTime En(int h, int m) => new(2026, 3, 15, h, m, 0);
    private static DateTime EnDiaSiguiente(int h, int m) => new(2026, 3, 16, h, m, 0);

    // Constantes con nombres semanticos para marcaciones frecuentes
    private static readonly MarcacionNormalizada Marcacion05_50 = new(En(5, 50), null);
    private static readonly MarcacionNormalizada Marcacion12_05 = new(En(12, 5), null);

    // Factory inline para marcaciones del dia base
    private static MarcacionNormalizada M(int h, int m) => new(En(h, m), null);

    // Factory inline para marcaciones del dia siguiente
    private static MarcacionNormalizada MSig(int h, int m) => new(EnDiaSiguiente(h, m), null);

    [Fact]
    public void Depurar_ProduceEntradaYSalidaCorrectas_CuandoFranjaUnicaConDosMarcaciones()
    {
        // CA-1: Una franja (06:00-14:00) con dos marcaciones (07:00, 15:00)
        // Con franja unica el rango es (-inf, +inf) -> primera=Entrada, ultima=Salida
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(7, 0), M(15, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(7, 0), En(15, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_DistribuyeMarcacionesPorPuntoDeCorte_CuandoTurnoPartido()
    {
        // CA-2: Turno partido (06:00-12:00, 14:00-18:00) con 4 marcaciones (05:50, 12:05, 14:10, 18:15)
        // Gap 12:00-14:00 -> punto de corte = 13:00 (punto medio del gap)
        // F1 captura marcaciones antes de 13:00: 05:50 y 12:05
        // F2 captura marcaciones desde 13:00: 14:10 y 18:15
        var turno = new TurnoDiario("Partido", [Franja06_12, Franja14_18], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase,
            [Marcacion05_50, Marcacion12_05, M(14, 10), M(18, 15)]);

        resultado.Should().BeEquivalentTo(new[]
        {
            new ControlFranja(Franja06_12, En(5, 50), En(12, 5)),
            new ControlFranja(Franja14_18, En(14, 10), En(18, 15))
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_ProduceEntradaConSalidaNull_CuandoFranjaTieneUnaSolaMarcacion()
    {
        // CA-3: Franja con una sola marcacion -> Entrada poblada, Salida null, EsAnomala true
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(7, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(7, 0), null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void Depurar_ProduceEntradaYSalidaNull_CuandoFranjaSinMarcaciones()
    {
        // CA-4: Franja sin marcaciones -> Entrada null, Salida null, EsAnomala true
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, []);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, null, null) },
            options => options.WithStrictOrdering());
        resultado[0].EsAnomala.Should().BeTrue();
    }

    [Fact]
    public void Depurar_DescartaMarcacionesIntermedias_CuandoHayMasDeDosMarcaciones()
    {
        // CA-5: Franja unica con marcaciones 07:00, 10:00, 12:00, 15:00
        // -> Entrada=07:00, Salida=15:00; las de 10:00 y 12:00 se descartan
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase,
            [M(7, 0), M(10, 0), M(12, 0), M(15, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(7, 0), En(15, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_RetornaListaVacia_CuandoTurnoEsNull()
    {
        // CA-6: turno null -> lista vacia (Count == 0)
        var resultado = DepuradorDeMarcaciones.Depurar(null, FechaBase, [M(7, 0)]);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Depurar_ResuelveRangoNocturno_CuandoFranjaTieneDiaOffsetFin1()
    {
        // CA-7: Franja nocturna 22:00-06:00 con DiaOffsetFin=1, fecha ancla 2026-03-15
        // Rango resuelto: 2026-03-15 22:00 -> 2026-03-16 06:00
        // Marcacion 22:30 del 15 y 05:30 del 16 quedan en la misma franja
        var turno = new TurnoDiario("Nocturno", [Franja22_06_Nocturna], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(22, 30), MSig(5, 30)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja22_06_Nocturna, En(22, 30), EnDiaSiguiente(5, 30)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_AsignaTodasLasMarcacionesAFranjaUnica_CuandoHayUnaSolaFranja()
    {
        // CA-8: Con una sola franja, el rango es (-inf, +inf) -> todas las marcaciones le pertenecen.
        // Timestamps fuera del horario nominal de la franja para verificar que no se filtra por hora.
        var turno = new TurnoDiario("Diurno", [Franja06_14], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase, [M(3, 0), M(20, 0)]);

        resultado.Should().BeEquivalentTo(
            new[] { new ControlFranja(Franja06_14, En(3, 0), En(20, 0)) },
            options => options.WithStrictOrdering());
    }

    [Fact]
    public void Depurar_SegundaFranjaEsAnomala_CuandoSoloPrimeraFranjaTieneMarcaciones()
    {
        // CA-10: Turno partido donde solo la primera franja tiene marcaciones
        // F1 (06:00-12:00): Entrada=05:50, Salida=12:05 -> EsAnomala false
        // F2 (14:00-18:00): Entrada=null, Salida=null -> EsAnomala true
        var turno = new TurnoDiario("Partido", [Franja06_12, Franja14_18], "");

        var resultado = DepuradorDeMarcaciones.Depurar(turno, FechaBase,
            [Marcacion05_50, Marcacion12_05]);

        resultado.Should().BeEquivalentTo(new[]
        {
            new ControlFranja(Franja06_12, En(5, 50), En(12, 5)),
            new ControlFranja(Franja14_18, null, null)
        }, options => options.WithStrictOrdering());
        resultado[1].EsAnomala.Should().BeTrue();
    }
}
