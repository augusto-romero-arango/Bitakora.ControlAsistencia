// Convencion: los trabajados se construyen con MomentoDelDia; ningun test construye DateTime.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class ClasificadorTrabajoTests
{
    private static readonly DateOnly Lunes = new(2026, 3, 16);    // habil no festivo
    private static readonly DateOnly Domingo = new(2026, 3, 15);  // domingo
    private static readonly DateOnly Sabado = new(2026, 3, 14);   // sabado; cruza medianoche hacia domingo

    private static readonly Func<DateOnly, bool> NingunFestivo = _ => false;
    private static readonly Func<DateOnly, bool> TodoFestivo = _ => true;

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
    public void Clasificar_DesglosaOrdinariaConDescanso_CuandoFranjaDiurnaSeTrabajaCompleta()
    {
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
    public void Clasificar_NoDevuelveIntervalos_CuandoTodoElTrabajoCaeAntesDelInicioProgramado()
    {
        // Marcaciones de cobertura anticipada (CA-ADR-0042): franja 6-14, trabajado [04:00,04:50].
        var resultado = ClasificadorTrabajo.Clasificar(
            Franja(6, 14), Intervalo(M(4), M(4, 50)), Lunes, NingunFestivo);

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Clasificar_CubreTrabajoMenosMinutosPrevios_CuandoHayEntradaTemprana()
    {
        // Invariante: sum(intervalos) == trabajado - minutosPrevios (60, entrada 07:00 con franja desde 08:00).
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
        // El tipo de dia se evalua por segmento: antes de medianoche domingo (festiva), despues lunes
        // habil (ordinaria).
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
        // Inverso del anterior: sabado habil antes de medianoche, domingo festivo despues.
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
        // Domingo y festivo a la vez: mismos conceptos que un domingo no festivo, sin duplicar recargo.
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
        var programada = Franja(8, 17, descansos: [Sub(12, 13)]);
        var trabajado = Intervalo(M(14), M(17));

        var resultado = ClasificadorTrabajo.Clasificar(programada, trabajado, Lunes, NingunFestivo);

        resultado.Should().Equal(new[] { Clasif(M(14), M(17), Concepto.OrdinariaDiurna) });
        resultado.Should().NotContain(i => i.Concepto == Concepto.Descanso);
    }

    [Fact]
    public void Clasificar_ClasificaSubFranjaExtraComoExtra_CuandoExtraCaeDentroDelRangoTrabajado()
    {
        // La extra programada se clasifica ExtraDiurna aunque caiga dentro del rango trabajado normal.
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
        // Ejercita las dos ramas de MapearConcepto que ningun otro caso cubre: ExtraDiurnaDominicalFestiva
        // y ExtraNocturnaDominicalFestiva.
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
