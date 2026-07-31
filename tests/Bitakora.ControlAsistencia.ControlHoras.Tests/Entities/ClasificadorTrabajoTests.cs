// Issue #135: Clasificar intervalos trabajados segun programacion de la franja.
// Tests directos sobre ClasificadorTrabajo.Clasificar - sin harness de event sourcing
// (es logica pura, sin interaccion de aggregate). Convencion de datos: todos los
// trabajados se construyen via IntervaloTemporal.Crear(new MomentoDelDia(...), ...).
// No se construye ningun DateTime en los tests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

/// <summary>
/// Tests de ClasificadorTrabajo.Clasificar. Cubre CA-1 a CA-13:
/// caso base + cobertura, cruce de frontera nocturna, cruce de medianoche con cambio de
/// tipo de dia, dia festivo, descansos programados, extras programadas, entrada temprana
/// y excedente (salida tardia sin retardo).
/// Fechas ancla fijas segun el issue:
///   2026-03-16 = lunes habil
///   2026-03-15 = domingo
///   2026-03-14 = sabado (su dia siguiente, domingo, sirve para el cruce de medianoche)
/// </summary>
public class ClasificadorTrabajoTests
{
    private static readonly DateOnly Lunes = new(2026, 3, 16);    // habil no festivo
    private static readonly DateOnly Domingo = new(2026, 3, 15);  // domingo
    private static readonly DateOnly Sabado = new(2026, 3, 14);   // sabado; cruza medianoche hacia domingo

    private static readonly Func<DateOnly, bool> NingunFestivo = _ => false;
    private static readonly Func<DateOnly, bool> TodoFestivo = _ => true;

    // Helpers de construccion (siempre en MomentoDelDia, nunca DateTime).
    private static MomentoDelDia M(int hora, int minuto = 0, int diaOffset = 0) =>
        new(new TimeOnly(hora, minuto), diaOffset);

    private static IntervaloTemporal Intervalo(MomentoDelDia inicio, MomentoDelDia fin) =>
        IntervaloTemporal.Crear(inicio, fin);

    private static IntervaloClasificado Clasif(MomentoDelDia inicio, MomentoDelDia fin, Concepto concepto) =>
        new(IntervaloTemporal.Crear(inicio, fin), concepto);

    private static DetalleSubFranja Sub(int horaInicio, int horaFin, int diaOffsetInicio = 0, int diaOffsetFin = 0) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetInicio, diaOffsetFin);

    private static DetalleFranjaOrdinaria Franja(
        int horaInicio,
        int horaFin,
        int diaOffsetFin = 0,
        IReadOnlyList<DetalleSubFranja>? descansos = null,
        IReadOnlyList<DetalleSubFranja>? extras = null) =>
        new(new TimeOnly(horaInicio, 0), new TimeOnly(horaFin, 0), diaOffsetFin,
            descansos ?? [], extras ?? []);

    [Fact]
    public void Clasificar_DesglosaOrdinariaConDescanso_CuandoFranjaDiurnaSeTrabajaCompleta()
    {
        // CA-1: franja 8-17 con descanso 12-13, trabajado [08:00,17:00], lunes habil -> 3 intervalos.
        var programada = Franja(8, 17, descansos: [Sub(12, 13)]);
        var trabajado = Intervalo(M(8), M(17));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(12), Concepto.OrdinariaDiurna),
            Clasif(M(12), M(13), Concepto.Descanso),
            Clasif(M(13), M(17), Concepto.OrdinariaDiurna),
        });
    }

    [Fact]
    public void Clasificar_CubreTrabajoMenosMinutosPrevios_CuandoHayEntradaTemprana()
    {
        // CA-2: invariante de cobertura. Entrada 07:00 con franja desde 08:00 => minutosPrevios = 60.
        // sum(intervalos.DuracionEnMinutos) == trabajado.DuracionEnMinutos - minutosPrevios.
        var programada = Franja(8, 17, descansos: [Sub(12, 13)]);
        var inicioTrabajado = M(7);
        var inicioFranja = M(8);
        var trabajado = Intervalo(inicioTrabajado, M(17));
        var minutosPrevios = Math.Max(0, inicioFranja.MinutosAbsolutos - inicioTrabajado.MinutosAbsolutos);

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Sum(i => i.DuracionEnMinutos)
            .Should().Be(trabajado.DuracionEnMinutos - minutosPrevios);
    }

    [Fact]
    public void Clasificar_SegmentaPorBandaHoraria_CuandoFranjaCruzaFronteraNocturna()
    {
        // CA-3: franja 14-22 cruza la frontera nocturna de las 19:00, dia habil.
        var programada = Franja(14, 22);
        var trabajado = Intervalo(M(14), M(22));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(14), M(19), Concepto.OrdinariaDiurna),
            Clasif(M(19), M(22), Concepto.OrdinariaNocturna),
        });
    }

    [Fact]
    public void Clasificar_EvaluaTipoDiaPorSegmento_CuandoCruzaMedianocheDesdeDomingo()
    {
        // CA-4: franja 22:00-06:00+1, ancla domingo. Pre-medianoche domingo (festivo) -> nocturna festiva;
        // post-medianoche lunes habil -> nocturna ordinaria. El tipo de dia se evalua por segmento.
        var programada = Franja(22, 6, diaOffsetFin: 1);
        var trabajado = Intervalo(M(22), M(6, 0, 1));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Domingo, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(22), M(0, 0, 1), Concepto.DominicalFestivaNocturna),
            Clasif(M(0, 0, 1), M(6, 0, 1), Concepto.OrdinariaNocturna),
        });
    }

    [Fact]
    public void Clasificar_EvaluaTipoDiaPorSegmento_CuandoCruzaMedianocheHaciaDomingo()
    {
        // CA-5: franja 22:00-06:00+1, ancla sabado. Inverso del CA-4: pre-medianoche sabado habil ->
        // nocturna ordinaria; post-medianoche domingo (festivo) -> nocturna festiva.
        var programada = Franja(22, 6, diaOffsetFin: 1);
        var trabajado = Intervalo(M(22), M(6, 0, 1));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Sabado, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(22), M(0, 0, 1), Concepto.OrdinariaNocturna),
            Clasif(M(0, 0, 1), M(6, 0, 1), Concepto.DominicalFestivaNocturna),
        });
    }

    [Fact]
    public void Clasificar_AplicaRecargoDominicalFestivo_CuandoDiaEsFestivoNoDomingo()
    {
        // CA-6: franja 8-17 con descanso 12-13 en festivo (lunes 2026-03-16, esFestivo => true).
        // Todos los segmentos de trabajo usan conceptos DominicalFestiva*, no Ordinaria*.
        var programada = Franja(8, 17, descansos: [Sub(12, 13)]);
        var trabajado = Intervalo(M(8), M(17));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, TodoFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(12), Concepto.DominicalFestivaDiurna),
            Clasif(M(12), M(13), Concepto.Descanso),
            Clasif(M(13), M(17), Concepto.DominicalFestivaDiurna),
        });
        resultado.Should().NotContain(i => i.Concepto == Concepto.OrdinariaDiurna);
    }

    [Fact]
    public void Clasificar_NoDuplicaRecargo_CuandoDiaEsDomingoYFestivoSimultaneamente()
    {
        // CA-7: un dia que es domingo y festivo a la vez produce conceptos DominicalFestiva* iguales
        // a los de un domingo no festivo (sin duplicar recargo).
        var programada = Franja(8, 17);
        var trabajado = Intervalo(M(8), M(17));

        var domingoNormal = ClasificadorTrabajo.Clasificar(programada, trabajado, Domingo, NingunFestivo);
        var domingoYFestivo = ClasificadorTrabajo.Clasificar(programada, trabajado, Domingo, TodoFestivo);

        domingoNormal.Should().Equal(new[] { Clasif(M(8), M(17), Concepto.DominicalFestivaDiurna) });
        domingoYFestivo.Should().Equal(domingoNormal);
    }

    [Fact]
    public void Clasificar_IntercalaDescansoEntreOrdinarias_CuandoDescansoEstaDentroDeLaFranja()
    {
        // CA-8: descanso 11-12 dentro de la franja 8-17 aparece como Descanso, con los minutos
        // adyacentes clasificados como ordinarios.
        var programada = Franja(8, 17, descansos: [Sub(11, 12)]);
        var trabajado = Intervalo(M(8), M(17));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(11), Concepto.OrdinariaDiurna),
            Clasif(M(11), M(12), Concepto.Descanso),
            Clasif(M(12), M(17), Concepto.OrdinariaDiurna),
        });
    }

    [Fact]
    public void Clasificar_OmiteDescanso_CuandoEntradaEsPosteriorAlFinDelDescanso()
    {
        // CA-9: trabajado [14:00,17:00]; el descanso 12-13 ya paso => no aparece en el desglose.
        var programada = Franja(8, 17, descansos: [Sub(12, 13)]);
        var trabajado = Intervalo(M(14), M(17));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[] { Clasif(M(14), M(17), Concepto.OrdinariaDiurna) });
        resultado.Should().NotContain(i => i.Concepto == Concepto.Descanso);
    }

    [Fact]
    public void Clasificar_ClasificaSubFranjaExtraComoExtra_CuandoExtraCaeDentroDelRangoTrabajado()
    {
        // CA-10: extra programada 17-18 como sub-franja de la franja 8-18 se clasifica como ExtraDiurna
        // aunque caiga dentro del rango trabajado normal.
        var programada = Franja(8, 18, extras: [Sub(17, 18)]);
        var trabajado = Intervalo(M(8), M(18));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(17), Concepto.OrdinariaDiurna),
            Clasif(M(17), M(18), Concepto.ExtraDiurna),
        });
    }

    [Fact]
    public void Clasificar_OmiteMinutosPrevios_CuandoEntradaEsAnteriorAlInicioDeFranja()
    {
        // CA-11: trabajado [07:00,17:00] con franja desde 08:00 => el intervalo 07:00-08:00 NO aparece;
        // el primer intervalo del desglose empieza en 08:00.
        var programada = Franja(8, 17, descansos: [Sub(12, 13)]);
        var trabajado = Intervalo(M(7), M(17));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(12), Concepto.OrdinariaDiurna),
            Clasif(M(12), M(13), Concepto.Descanso),
            Clasif(M(13), M(17), Concepto.OrdinariaDiurna),
        });
    }

    [Fact]
    public void Clasificar_ClasificaExcedenteComoExtra_CuandoSalidaSuperaElFinDeFranja()
    {
        // CA-12: franja 8-17, trabajado [08:00,18:00], dia habil => excedente 17:00-18:00 como ExtraDiurna.
        var programada = Franja(8, 17);
        var trabajado = Intervalo(M(8), M(18));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(17), Concepto.OrdinariaDiurna),
            Clasif(M(17), M(18), Concepto.ExtraDiurna),
        });
    }

    [Fact]
    public void Clasificar_SegmentaExcedentePorBanda_CuandoExcedenteCruzaFronteraNocturna()
    {
        // CA-13: franja 8-17, trabajado [08:00,20:00], dia habil => excedente segmentado por banda:
        // 17:00-19:00 ExtraDiurna y 19:00-20:00 ExtraNocturna.
        var programada = Franja(8, 17);
        var trabajado = Intervalo(M(8), M(20));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(17), Concepto.OrdinariaDiurna),
            Clasif(M(17), M(19), Concepto.ExtraDiurna),
            Clasif(M(19), M(20), Concepto.ExtraNocturna),
        });
    }

    [Fact]
    public void Clasificar_ClasificaExtrasComoDominicalFestivas_CuandoExcedenteOcurreEnDomingo()
    {
        // Cierra la cobertura del mapeo: combina excedente (Extra) con tipo de dia DominicalFestivo,
        // las dos unicas ramas de MapearConcepto que ningun CA numerado ejercita
        // (ExtraDiurnaDominicalFestiva y ExtraNocturnaDominicalFestiva). Franja 8-17 trabajada
        // [08:00,20:00] en domingo => ordinaria festiva + excedente festivo segmentado por banda.
        var programada = Franja(8, 17);
        var trabajado = Intervalo(M(8), M(20));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Domingo, NingunFestivo);

        resultado.Should().Equal(new[]
        {
            Clasif(M(8), M(17), Concepto.DominicalFestivaDiurna),
            Clasif(M(17), M(19), Concepto.ExtraDiurnaDominicalFestiva),
            Clasif(M(19), M(20), Concepto.ExtraNocturnaDominicalFestiva),
        });
    }
}
