// Issue #136: Calcular retardo, compensacion intra-franja y ensamblar DesgloseFranja.
// Capa 4 (Parte D, final) del desglose de horas. Tests directos sobre
// ControlFranja.CalcularDesglose - logica pura sin harness de event sourcing.
//
// Convencion de datos (issue #136): los ControlFranja se construyen con DateTime? (frontera
// del dominio: las marcaciones llegan como DateTime), pero TODAS las aserciones sobre
// resultados se hacen en MomentoDelDia / IntervaloTemporal / Retardo. El DateTime solo
// aparece al construir la entrada del metodo.
//
// Retardo (#114) solo expone RetardoNeto publicamente (ADR-0015, Tell-don't-Ask): los
// minutos retardados/compensados son detalle interno. Por eso el retardo se verifica
// construyendo el Retardo esperado via Retardo.Crear(...) y comparando por
// igualdad (que internamente compara esos minutos y los intervalos), mas RetardoNeto.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

/// <summary>
/// Tests de ControlFranja.CalcularDesglose. Cubre CA-1 a CA-8: compuerta de anomala, retardo
/// simple sin excedente, puntualidad, excedente sin retardo, compensacion intra-franja (incluido
/// el cruce de frontera nocturna) y el invariante de cobertura. Los escenarios base del desglose
/// (segmentacion, festivos, descansos) ya estan cubiertos en ClasificadorTrabajoTests (#135).
///
/// Fecha ancla fija: 2026-03-16 = lunes habil. Todas las marcaciones de los CA caen ese dia.
/// </summary>
public class CalcularDesgloseFranjaTests
{
    private static readonly DateOnly Lunes = new(2026, 3, 16); // habil no festivo

    private static readonly Func<DateOnly, bool> NingunFestivo = _ => false;

    // Construccion de la entrada del metodo: ControlFranja recibe DateTime? (frontera del dominio).
    private static DateTime Dt(int hora, int minuto = 0) => new(2026, 3, 16, hora, minuto, 0);

    private static ControlFranja Control(DetalleFranjaOrdinaria programada, DateTime? entrada, DateTime? salida) =>
        new(programada, entrada, salida);

    // Helpers de asercion: siempre en MomentoDelDia / IntervaloTemporal, nunca DateTime.
    private static MomentoDelDia M(int hora, int minuto = 0, int diaOffset = 0) =>
        new(new TimeOnly(hora, minuto), diaOffset);

    private static IntervaloTemporal Intervalo(MomentoDelDia inicio, MomentoDelDia fin) =>
        IntervaloTemporal.Crear(inicio, fin);

    private static IntervaloClasificado Clasif(MomentoDelDia inicio, MomentoDelDia fin, Concepto concepto) =>
        new(IntervaloTemporal.Crear(inicio, fin), concepto);

    // Issue #288: Descripcion (dato derivado) es irrelevante para estos tests -> placeholder "".
    private static DetalleSubFranja Sub(int horaInicio, int horaFin, int diaOffsetInicio = 0, int diaOffsetFin = 0) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetInicio, diaOffsetFin, "");

    private static DetalleFranjaOrdinaria Franja(
        int horaInicio,
        int horaFin,
        int diaOffsetFin = 0,
        IReadOnlyList<DetalleSubFranja>? descansos = null,
        IReadOnlyList<DetalleSubFranja>? extras = null) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetFin,
            descansos ?? [], extras ?? [], "");

    // ----- CA-1: compuerta de anomala -----

    [Fact]
    public void CalcularDesglose_RetornaNull_CuandoEntradaEsNula()
    {
        // CA-1: sin entrada => EsAnomala => CalcularDesglose retorna null (no se intenta desglosar).
        var control = Control(Franja(8, 17), entrada: null, salida: Dt(17));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        control.EsAnomala.Should().BeTrue();
        desglose.Should().BeNull();
    }

    [Fact]
    public void CalcularDesglose_RetornaNull_CuandoSalidaEsNula()
    {
        // CA-1: sin salida => EsAnomala => CalcularDesglose retorna null.
        var control = Control(Franja(8, 17), entrada: Dt(8), salida: null);

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        control.EsAnomala.Should().BeTrue();
        desglose.Should().BeNull();
    }

    // ----- CA-2, CA-3: retardo simple (sin excedente) -----

    [Fact]
    public void CalcularDesglose_RegistraRetardoSinCompensacion_CuandoEntradaLlegaTarde()
    {
        // CA-2: franja 8-17, entrada 08:30 (30min tarde), salida 17:00 puntual, dia habil.
        // Retardo = 30, sin excedente => sin compensacion. Los Intervalos arrancan en la entrada
        // efectiva 08:30 (no incluyen el tiempo previo). TiempoRetardado = [08:00-08:30].
        var control = Control(Franja(8, 17), Dt(8, 30), Dt(17));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Programada.Should().Be(Franja(8, 17));
        desglose.Intervalos.Should().Equal(new[]
        {
            Clasif(M(8, 30), M(17), Concepto.OrdinariaDiurna),
        });
        desglose.Retardo.Should().Be(Retardo.Crear(
            tiempoRetardado: [Intervalo(M(8), M(8, 30))],
            tiempoCompensado: []));
        desglose.Retardo.RetardoNeto.Should().Be(30);
    }

    [Fact]
    public void CalcularDesglose_NoRegistraRetardo_CuandoEntradaEsPuntual()
    {
        // CA-3: franja 8-17, entrada 08:00 puntual, salida 17:00 => retardo 0 y sin excedente.
        // Retardo == Retardo.Vacio.
        var control = Control(Franja(8, 17), Dt(8), Dt(17));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Intervalos.Should().Equal(new[]
        {
            Clasif(M(8), M(17), Concepto.OrdinariaDiurna),
        });
        desglose.Retardo.Should().Be(Retardo.Vacio);
        desglose.Retardo.RetardoNeto.Should().Be(0);
    }

    // ----- CA-4: excedente sin retardo -----

    [Fact]
    public void CalcularDesglose_ClasificaExcedenteComoExtra_CuandoSalidaSeProlongaSinRetardo()
    {
        // CA-4: franja 8-17, entrada 08:00, salida 18:00, dia habil => excedente 17:00-18:00
        // clasificado como ExtraDiurna (herencia de #135). Sin retardo => sin compensacion, la
        // extra sobrevive completa. Retardo == Vacio.
        var control = Control(Franja(8, 17), Dt(8), Dt(18));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Intervalos.Should().Equal(new[]
        {
            Clasif(M(8), M(17), Concepto.OrdinariaDiurna),
            Clasif(M(17), M(18), Concepto.ExtraDiurna),
        });
        desglose.Retardo.Should().Be(Retardo.Vacio);
        desglose.Retardo.RetardoNeto.Should().Be(0);
    }

    // ----- CA-5, CA-6: compensacion intra-franja -----

    [Fact]
    public void CalcularDesglose_CompensaExcedenteConRetardo_CuandoHayAmbosEnLaFranja()
    {
        // CA-5: franja 8-17, entrada 08:30, salida 18:00, dia habil. Retardo = 30, excedente bruto
        // = 60. Compensacion = min(30, 60) = 30 desde el final: parte la extra 17:00-18:00 en
        // 17:00-17:30 (visible) + 17:30-18:00 (compensado). Este CA ejerce IntervaloTemporal.Partir.
        // RetardoNeto = 0 (todo el retardo queda compensado).
        var control = Control(Franja(8, 17), Dt(8, 30), Dt(18));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Intervalos.Should().Equal(new[]
        {
            Clasif(M(8, 30), M(17), Concepto.OrdinariaDiurna),
            Clasif(M(17), M(17, 30), Concepto.ExtraDiurna),
        });
        desglose.Retardo.Should().Be(Retardo.Crear(
            tiempoRetardado: [Intervalo(M(8), M(8, 30))],
            tiempoCompensado: [Intervalo(M(17, 30), M(18))]));
        desglose.Retardo.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void CalcularDesglose_TopaCompensacionAlExcedente_CuandoRetardoSuperaExcedente()
    {
        // CA-6: franja 8-17, entrada 09:00, salida 17:30. Retardo = 60, excedente bruto = 30.
        // Compensacion = min(60, 30) = 30 => la unica extra 17:00-17:30 se consume completa y
        // desaparece de los Intervalos. RetardoNeto = 60 - 30 = 30.
        var control = Control(Franja(8, 17), Dt(9), Dt(17, 30));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Intervalos.Should().Equal(new[]
        {
            Clasif(M(9), M(17), Concepto.OrdinariaDiurna),
        });
        desglose.Intervalos.Should().NotContain(i => i.Concepto == Concepto.ExtraDiurna);
        desglose.Retardo.Should().Be(Retardo.Crear(
            tiempoRetardado: [Intervalo(M(8), M(9))],
            tiempoCompensado: [Intervalo(M(17), M(17, 30))]));
        desglose.Retardo.RetardoNeto.Should().Be(30);
    }

    // ----- CA-7: compensacion cruzando frontera nocturna -----

    [Fact]
    public void CalcularDesglose_ConsumeExtraNocturnaDesdeElFinal_CuandoCompensacionCruzaFronteraNocturna()
    {
        // CA-7: franja 8-17, entrada 08:30, salida 19:30, dia habil. Retardo = 30, excedente bruto
        // = 150 (17:00-19:00 ExtraDiurna 120min + 19:00-19:30 ExtraNocturna 30min). La compensacion
        // de 30min consume desde el final exactamente la franja nocturna 19:00-19:30 completa: la
        // extra nocturna desaparece (solo existia por el retardo) y la diurna 17:00-19:00 sobrevive.
        var control = Control(Franja(8, 17), Dt(8, 30), Dt(19, 30));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Intervalos.Should().Equal(new[]
        {
            Clasif(M(8, 30), M(17), Concepto.OrdinariaDiurna),
            Clasif(M(17), M(19), Concepto.ExtraDiurna),
        });
        desglose.Intervalos.Should().NotContain(i => i.Concepto == Concepto.ExtraNocturna);
        desglose.Retardo.Should().Be(Retardo.Crear(
            tiempoRetardado: [Intervalo(M(8), M(8, 30))],
            tiempoCompensado: [Intervalo(M(19), M(19, 30))]));
        desglose.Retardo.RetardoNeto.Should().Be(0);
    }

    [Fact]
    public void CalcularDesglose_DevuelveCompensacionEnDosIntervalosAscendentes_CuandoElConsumoParteLaFranjaDiurna()
    {
        // Refuerzo de CA-7: el caso multi-intervalo descrito en "Estructura de Retardo
        // producido". Franja 8-17, entrada 09:30 (retardo 90), salida 19:30. Excedente bruto = 150
        // (17:00-19:00 ExtraDiurna 120min + 19:00-19:30 ExtraNocturna 30min). Compensacion = min(90,150)
        // = 90: consume entera la nocturna 19:00-19:30 (30) y PARTE la diurna 17:00-19:00 en 18:00 - la
        // izquierda 17:00-18:00 sobrevive como extra visible y la derecha 18:00-19:00 se compensa. Asi
        // el TiempoCompensado queda con DOS intervalos en orden cronologico ascendente
        // [18:00-19:00, 19:00-19:30]. Este test ancla el contrato de orden (el Reverse interno del
        // helper) que los demas CA no ejercen porque su compensacion cabe en un solo intervalo.
        var control = Control(Franja(8, 17), Dt(9, 30), Dt(19, 30));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Intervalos.Should().Equal(new[]
        {
            Clasif(M(9, 30), M(17), Concepto.OrdinariaDiurna),
            Clasif(M(17), M(18), Concepto.ExtraDiurna),
        });
        desglose.Intervalos.Should().NotContain(i => i.Concepto == Concepto.ExtraNocturna);
        desglose.Retardo.Should().Be(Retardo.Crear(
            tiempoRetardado: [Intervalo(M(8), M(9, 30))],
            tiempoCompensado: [Intervalo(M(18), M(19)), Intervalo(M(19), M(19, 30))]));
        desglose.Retardo.RetardoNeto.Should().Be(0);
    }

    // ----- CA-8: invariante de cobertura -----

    [Fact]
    public void CalcularDesglose_PreservaInvarianteDeCobertura_CuandoHayCompensacion()
    {
        // CA-8: sum(Intervalos.DuracionEnMinutos) + minutosCompensados == trabajadoEfectivo.
        // Reusa el escenario de CA-5 (entrada 08:30, salida 18:00). La entrada efectiva tras el
        // recorte es 08:30 (ya posterior al inicio de franja), asi que trabajadoEfectivo = 08:30-18:00.
        // Los minutos compensados se derivan del Retardo esperado (TiempoCompensado), cuya
        // igualdad ancla el test: 17:30-18:00 = 30min.
        var control = Control(Franja(8, 17), Dt(8, 30), Dt(18));
        var trabajadoEfectivo = Intervalo(M(8, 30), M(18));
        var tiempoCompensado = new[] { Intervalo(M(17, 30), M(18)) };
        var minutosCompensados = tiempoCompensado.Sum(i => i.DuracionEnMinutos);

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        // Ancla: el TiempoCompensado del resultado es exactamente el que sumamos en el invariante.
        desglose!.Retardo.Should().Be(Retardo.Crear(
            tiempoRetardado: [Intervalo(M(8), M(8, 30))],
            tiempoCompensado: tiempoCompensado));
        (desglose.Intervalos.Sum(i => i.DuracionEnMinutos) + minutosCompensados)
            .Should().Be(trabajadoEfectivo.DuracionEnMinutos);
    }
}
