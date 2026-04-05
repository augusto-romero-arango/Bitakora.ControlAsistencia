// Issue #2: Modelar value objects de franja temporal para turnos
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;

namespace Bitakora.ControlAsistencia.Contracts.Tests.ValueObjects;

/// <summary>
/// Tests de FranjaOrdinaria - franja principal que define el horario de trabajo.
/// Interfaz publica: Crear(...), DuracionEnMinutos(), DuracionEnHorasDecimales(), ToString().
/// Contencion y solapamiento se validan dentro del factory (CA-14 a CA-19).
/// El exito del factory se verifica con ToString() que expone la estructura de hijos.
/// </summary>
public class FranjaOrdinariaTests
{
    // ---------- CA-8: factory estatico - camino feliz ----------

    [Fact]
    public void Crear_RetornaFranjaOrdinariaSinHijos_CuandoDatosValidosSinHijos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        franja.ToString().Should().Be("(06:00-12:00)");
    }

    [Fact]
    public void Crear_RetornaFranjaOrdinariaConDescanso_CuandoDescansoEstaContenido()
    {
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso]);

        franja.ToString().Should().Be("(06:00-12:00)[Descansos:(10:00-10:15)]");
    }

    [Fact]
    public void Crear_RetornaFranjaOrdinariaConExtra_CuandoExtraEstaContenida()
    {
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            extras: [extra]);

        franja.ToString().Should().Be("(06:00-12:00)[Extras:(06:00-08:00)]");
    }

    [Fact]
    public void Crear_RetornaFranjaOrdinariaConDescansosYExtras_CuandoTodosEsanContenidos()
    {
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso],
            extras: [extra]);

        franja.ToString().Should().Be("(06:00-12:00)[Descansos:(10:00-10:15)][Extras:(06:00-08:00)]");
    }

    // ---------- CA-7: inicio == fin es rechazado ----------

    [Fact]
    public void Crear_LanzaExcepcion_CuandoInicioYFinSonIguales()
    {
        var act = () => FranjaOrdinaria.Crear(new TimeOnly(10, 0), new TimeOnly(10, 0));

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.InicioYFinIguales}*");
    }

    // ---------- CA-10: duracion sin offset ----------

    [Fact]
    public void DuracionEnMinutos_Retorna180_CuandoFranja8A11()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(11, 0));

        franja.DuracionEnMinutos().Should().Be(180);
    }

    // ---------- CA-6: semantica inicio-inclusivo, fin-exclusivo ----------

    [Fact]
    public void DuracionEnMinutos_Retorna360_CuandoFranja6A12Exactos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(12, 0));

        franja.DuracionEnMinutos().Should().Be(360);
    }

    // ---------- CA-11: duracion con offset de dia ----------

    [Fact]
    public void DuracionEnMinutos_Retorna480_CuandoFranjaNocturna22A6ConOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.DuracionEnMinutos().Should().Be(480);
    }

    // ---------- CA-12: conversores de duracion ----------

    [Fact]
    public void DuracionEnHorasDecimales_Retorna3Punto5_CuandoFranjaDe210Minutos()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(8, 0), new TimeOnly(11, 30));

        franja.DuracionEnHorasDecimales().Should().Be(3.5);
    }

    // ---------- CA-13: infiere offset +1 cuando fin < inicio ----------

    [Fact]
    public void Crear_InfiereOffsetMasUno_CuandoFinEsMenorQueInicio()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        franja.ToString().Should().Be("(22:00-06:00+1)");
    }

    [Fact]
    public void DuracionEnMinutos_Retorna480_CuandoOffsetInferido()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0));

        franja.DuracionEnMinutos().Should().Be(480);
    }

    // ---------- CA-20, CA-21: ToString() ----------

    [Fact]
    public void ToString_MuestraFormatoConParentesis_SinOffset()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(18, 0));

        franja.ToString().Should().Be("(14:00-18:00)");
    }

    [Fact]
    public void ToString_MuestraOffsetMasUno_CuandoDiaOffsetFinEsUno()
    {
        var franja = FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1);

        franja.ToString().Should().Be("(22:00-06:00+1)");
    }

    // ---------- CA-14: contencion - descanso no contenido es rechazado ----------

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDescansoExcedeFin()
    {
        // descanso termina a las 13:00, ordinaria termina a las 12:00
        var descanso = FranjaDescanso.Crear(new TimeOnly(11, 0), new TimeOnly(13, 0));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDescansoEmpiezaAntesDeInicio()
    {
        // descanso empieza a las 7:00, ordinaria empieza a las 8:00
        var descanso = FranjaDescanso.Crear(new TimeOnly(7, 0), new TimeOnly(9, 0));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(8, 0), new TimeOnly(12, 0),
            descansos: [descanso]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoExtraExcedeFin()
    {
        // extra termina a las 13:00, ordinaria termina a las 12:00
        var extra = FranjaExtra.Crear(new TimeOnly(11, 0), new TimeOnly(13, 0));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            extras: [extra]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    // ---------- CA-15: contencion con cruce de medianoche ----------

    [Fact]
    public void Crear_RetornaFranjaConDescanso_CuandoDescansoEstaContenidoEnOrdinariaNocturna()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // Descanso en tramo nocturno antes de medianoche: 23:00+0 -> 23:15+0
        var descanso = FranjaDescanso.Crear(new TimeOnly(23, 0), new TimeOnly(23, 15),
            diaOffsetInicio: 0, diaOffsetFin: 0);

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            descansos: [descanso]);

        franja.ToString().Should().Be("(22:00-06:00+1)[Descansos:(23:00-23:15)]");
    }

    // ---------- CA-16: franja hija que excede ordinaria nocturna ----------

    [Fact]
    public void Crear_LanzaExcepcion_CuandoExtraExcedeLaOrdinariaNocturna()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // Extra: 05:00+1 -> 07:00+1 - termina despues de las 06:00+1
        var extra = FranjaExtra.Crear(new TimeOnly(5, 0), new TimeOnly(7, 0),
            diaOffsetInicio: 1, diaOffsetFin: 1);

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            extras: [extra]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjaHijaFueraDeContenedor}*");
    }

    // ---------- CA-17: solapamiento entre franjas hijas ----------

    [Fact]
    public void Crear_LanzaExcepcion_CuandoExtraYDescansoSeSolapan()
    {
        // extra 09:00-11:00 y descanso 10:00-10:15 se solapan
        var extra = FranjaExtra.Crear(new TimeOnly(9, 0), new TimeOnly(11, 0));
        var descanso = FranjaDescanso.Crear(new TimeOnly(10, 0), new TimeOnly(10, 15));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso],
            extras: [extra]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
    }

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDosDescansosSeSolapan()
    {
        var descanso1 = FranjaDescanso.Crear(new TimeOnly(9, 0), new TimeOnly(10, 0));
        var descanso2 = FranjaDescanso.Crear(new TimeOnly(9, 30), new TimeOnly(10, 30));

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso1, descanso2]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
    }

    // ---------- CA-18: franjas contiguas NO se solapan (fin exclusivo) ----------

    [Fact]
    public void Crear_RetornaFranjaConHijosContiguos_CuandoFinDeUnoEsInicioDelOtro()
    {
        // extra termina exactamente donde empieza el descanso - contiguas, no solapadas
        var extra = FranjaExtra.Crear(new TimeOnly(6, 0), new TimeOnly(8, 0));
        var descanso = FranjaDescanso.Crear(new TimeOnly(8, 0), new TimeOnly(8, 15));

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(6, 0), new TimeOnly(12, 0),
            descansos: [descanso],
            extras: [extra]);

        franja.ToString().Should().Be("(06:00-12:00)[Descansos:(08:00-08:15)][Extras:(06:00-08:00)]");
    }

    // ---------- CA-19: solapamiento con offsets ----------

    [Fact]
    public void Crear_LanzaExcepcion_CuandoDosDescansosSeSolapanConOffsets()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // Dos descansos que se solapan en el tramo antes de medianoche
        var descanso1 = FranjaDescanso.Crear(new TimeOnly(23, 0), new TimeOnly(23, 30),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var descanso2 = FranjaDescanso.Crear(new TimeOnly(23, 15), new TimeOnly(23, 45),
            diaOffsetInicio: 0, diaOffsetFin: 0);

        var act = () => FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            descansos: [descanso1, descanso2]);

        act.Should().ThrowExactly<ArgumentException>()
            .WithMessage($"*{FranjaTemporal.Mensajes.FranjasHijasSeSuperponen}*");
    }

    [Fact]
    public void Crear_RetornaFranjaConHijosConOffsets_CuandoFranjasConContiguasConOffsets()
    {
        // Ordinaria nocturna: 22:00+0 -> 06:00+1
        // descanso termina exactamente donde empieza la extra - contiguas, no solapadas
        var descanso = FranjaDescanso.Crear(new TimeOnly(23, 0), new TimeOnly(23, 15),
            diaOffsetInicio: 0, diaOffsetFin: 0);
        var extra = FranjaExtra.Crear(new TimeOnly(23, 15), new TimeOnly(0, 0),
            diaOffsetInicio: 0, diaOffsetFin: 1);

        var franja = FranjaOrdinaria.Crear(
            new TimeOnly(22, 0), new TimeOnly(6, 0), diaOffsetFin: 1,
            descansos: [descanso],
            extras: [extra]);

        franja.ToString().Should().Be("(22:00-06:00+1)[Descansos:(23:00-23:15)][Extras:(23:15-00:00+1)]");
    }
}
