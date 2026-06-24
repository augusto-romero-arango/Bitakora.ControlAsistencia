// Issue #143: Alinear IntervaloTemporal con ADR-0015 y agregar factories de conversion al VO
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ValueObjects;

/// <summary>
/// Tests para Desde(DateTime, DateTime, DateOnly) y Partir(int) de IntervaloTemporal.
/// Interfaz publica nueva en Issue #143.
/// CA-13 a CA-18.
/// </summary>
public class IntervaloTemporalDesdeYPartirTests
{
    private static readonly DateOnly FechaBase = new(2026, 3, 15);
    private static readonly MomentoDelDia Las8 = new(new TimeOnly(8, 0));
    private static readonly MomentoDelDia Las830 = new(new TimeOnly(8, 30));
    private static readonly MomentoDelDia Las930 = new(new TimeOnly(9, 30));
    private static readonly MomentoDelDia Las10 = new(new TimeOnly(10, 0));

    // ---------- CA-13: Desde rango diurno mismo dia ----------

    [Fact]
    public void Desde_RetornaIntervaloEquivalenteAlEsperado_CuandoRangoDiurnoMismoDia()
    {
        var intervalo = IntervaloTemporal.Desde(
            new DateTime(2026, 3, 15, 8, 0, 0),
            new DateTime(2026, 3, 15, 17, 0, 0),
            FechaBase);

        var esperado = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(8, 0), 0),
            new MomentoDelDia(new TimeOnly(17, 0), 0));
        intervalo.Should().Be(esperado);
        intervalo.DuracionEnMinutos.Should().Be(540);
    }

    // ---------- CA-14: Desde cruzando medianoche ----------

    [Fact]
    public void Desde_RetornaIntervaloConFinEnDiaSiguiente_CuandoRangoCruzaMedianoche()
    {
        var intervalo = IntervaloTemporal.Desde(
            new DateTime(2026, 3, 15, 22, 0, 0),
            new DateTime(2026, 3, 16, 6, 0, 0),
            FechaBase);

        var esperado = IntervaloTemporal.Crear(
            new MomentoDelDia(new TimeOnly(22, 0), 0),
            new MomentoDelDia(new TimeOnly(6, 0), 1));
        intervalo.Should().Be(esperado);
        intervalo.DuracionEnMinutos.Should().Be(480);
    }

    // ---------- CA-15: Partir(30) ----------

    [Fact]
    public void Partir_RetornaParIzquierdoDerecho_CuandoSeParticionaA30Minutos()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las10);

        var (izquierdo, derecho) = intervalo.Partir(30);

        izquierdo.Should().Be(IntervaloTemporal.Crear(Las8, Las830));
        derecho.Should().Be(IntervaloTemporal.Crear(Las830, Las10));
    }

    // ---------- CA-16: Partir(90) ----------

    [Fact]
    public void Partir_RetornaParIzquierdoDerecho_CuandoSeParticionaA90Minutos()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las10);

        var (izquierdo, derecho) = intervalo.Partir(90);

        izquierdo.Should().Be(IntervaloTemporal.Crear(Las8, Las930));
        derecho.Should().Be(IntervaloTemporal.Crear(Las930, Las10));
    }

    // ---------- CA-17: Partir(0) rechaza cero ----------

    [Fact]
    public void Partir_LanzaArgumentException_CuandoPuntoDeParticionEsCero()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las10);

        var act = () => intervalo.Partir(0);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{IntervaloTemporal.Mensajes.PuntoDeParticionDebeSerInterior}*");
    }

    [Fact]
    public void Partir_LanzaArgumentException_CuandoPuntoDeParticionEsNegativo()
    {
        var intervalo = IntervaloTemporal.Crear(Las8, Las10);

        var act = () => intervalo.Partir(-1);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{IntervaloTemporal.Mensajes.PuntoDeParticionDebeSerInterior}*");
    }

    // ---------- CA-18: Partir(120) rechaza igual a duracion total ----------

    [Fact]
    public void Partir_LanzaArgumentException_CuandoPuntoDeParticionEsIgualADuracionTotal()
    {
        // Crear(08:00, 10:00) tiene DuracionEnMinutos = 120
        var intervalo = IntervaloTemporal.Crear(Las8, Las10);

        var act = () => intervalo.Partir(120);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{IntervaloTemporal.Mensajes.PuntoDeParticionDebeSerInterior}*");
    }
}
