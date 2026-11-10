// Las aserciones van en MomentoDelDia/IntervaloTemporal/Retardo; DateTime solo construye la entrada.
// Retardo solo expone RetardoNeto (Tell-don't-Ask): se verifica contra un Retardo.Crear(...) esperado,
// cuya igualdad compara tambien los intervalos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class CalcularDesgloseFranjaTests
{
    private static readonly DateOnly Lunes = new(2026, 3, 16); // habil no festivo

    private static readonly Func<DateOnly, bool> NingunFestivo = _ => false;

    private static DateTime Dt(int hora, int minuto = 0) => new(2026, 3, 16, hora, minuto, 0);

    private static ControlFranja Control(FranjaProgramada programada, DateTime? entrada, DateTime? salida) =>
        new(programada, entrada, salida);

    private static MomentoDelDia M(int hora, int minuto = 0, int diaOffset = 0) =>
        new(new TimeOnly(hora, minuto), diaOffset);

    private static IntervaloTemporal Intervalo(MomentoDelDia inicio, MomentoDelDia fin) =>
        IntervaloTemporal.Crear(inicio, fin);

    private static IntervaloClasificado Clasif(MomentoDelDia inicio, MomentoDelDia fin, Concepto concepto) =>
        new(IntervaloTemporal.Crear(inicio, fin), concepto);

    // Descripcion (dato derivado) no interviene en estos tests: placeholder "".
    private static SubFranjaProgramada Sub(int horaInicio, int horaFin, int diaOffsetInicio = 0, int diaOffsetFin = 0) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetInicio, diaOffsetFin, "");

    private static FranjaProgramada Franja(
        int horaInicio,
        int horaFin,
        int diaOffsetFin = 0,
        IReadOnlyList<SubFranjaProgramada>? descansos = null,
        IReadOnlyList<SubFranjaProgramada>? extras = null) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetFin,
            descansos ?? [], extras ?? [], "");

    [Fact]
    public void CalcularDesglose_RetornaNull_CuandoEntradaEsNula()
    {
        var control = Control(Franja(8, 17), entrada: null, salida: Dt(17));

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        control.EsAnomala.Should().BeTrue();
        desglose.Should().BeNull();
    }

    [Fact]
    public void CalcularDesglose_RetornaNull_CuandoSalidaEsNula()
    {
        var control = Control(Franja(8, 17), entrada: Dt(8), salida: null);

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        control.EsAnomala.Should().BeTrue();
        desglose.Should().BeNull();
    }

    [Fact]
    public void CalcularDesglose_RegistraRetardoSinCompensacion_CuandoEntradaLlegaTarde()
    {
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

    [Fact]
    public void CalcularDesglose_ClasificaExcedenteComoExtra_CuandoSalidaSeProlongaSinRetardo()
    {
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

    [Fact]
    public void CalcularDesglose_CompensaExcedenteConRetardo_CuandoHayAmbosEnLaFranja()
    {
        // Compensacion = min(retardo 30, excedente 60) = 30, consumida desde el final: parte la extra en
        // 17:00-17:30 (visible) + 17:30-18:00 (compensado).
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
        // Compensacion = min(60, 30) = 30: la extra se consume completa; RetardoNeto = 60 - 30.
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

    [Fact]
    public void CalcularDesglose_ConsumeExtraNocturnaDesdeElFinal_CuandoCompensacionCruzaFronteraNocturna()
    {
        // Los 30 min de compensacion consumen desde el final justo la extra nocturna 19:00-19:30; la
        // diurna sobrevive.
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
        // Compensacion 90 = nocturna entera (30) + parte de la diurna (60): TiempoCompensado queda con
        // dos intervalos en orden ascendente. Ancla el contrato de orden (el Reverse interno) que los
        // demas casos no ejercen.
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

    [Fact]
    public void CalcularDesglose_PreservaInvarianteDeCobertura_CuandoHayCompensacion()
    {
        // Invariante: sum(Intervalos) + minutosCompensados == trabajado efectivo (08:30-18:00).
        var control = Control(Franja(8, 17), Dt(8, 30), Dt(18));
        var trabajadoEfectivo = Intervalo(M(8, 30), M(18));
        var tiempoCompensado = new[] { Intervalo(M(17, 30), M(18)) };
        var minutosCompensados = tiempoCompensado.Sum(i => i.DuracionEnMinutos);

        var desglose = control.CalcularDesglose(Lunes, NingunFestivo);

        desglose.Should().NotBeNull();
        desglose!.Retardo.Should().Be(Retardo.Crear(
            tiempoRetardado: [Intervalo(M(8), M(8, 30))],
            tiempoCompensado: tiempoCompensado));
        (desglose.Intervalos.Sum(i => i.DuracionEnMinutos) + minutosCompensados)
            .Should().Be(trabajadoEfectivo.DuracionEnMinutos);
    }
}
