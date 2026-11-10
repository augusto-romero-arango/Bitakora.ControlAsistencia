// Todos los offsets son relativos a la misma fecha ancla (la de asignacion), no al inicio de la franja:
// HoraInicio de la franja es offset 0, HoraFin usa DiaOffsetFin y cada sub-franja su DiaOffsetInicio/Fin.
// Descansos y extras no tienen sede propia: heredan la de su franja; una franja sin sede da bloques con
// sede null.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada.Eventos;

public class TurnoDiarioSegmentarTests
{
    private static readonly DateOnly Fecha = new(2026, 8, 10);

    private static SubFranjaProgramada SubFranja(
        int horaInicio, int minutoInicio, int horaFin, int minutoFin,
        int diaOffsetInicio = 0, int diaOffsetFin = 0) =>
        new(new TimeOnly(horaInicio, minutoInicio), new TimeOnly(horaFin, minutoFin),
            diaOffsetInicio, diaOffsetFin, "");

    private static FranjaProgramada Franja(
        int horaInicio, int minutoInicio, int horaFin, int minutoFin, int diaOffsetFin = 0,
        IReadOnlyList<SubFranjaProgramada>? descansos = null,
        IReadOnlyList<SubFranjaProgramada>? extras = null,
        SedeProgramada? sede = null) =>
        new(new TimeOnly(horaInicio, minutoInicio), new TimeOnly(horaFin, minutoFin), diaOffsetFin,
            descansos ?? [], extras ?? [], "", sede);

    private static TurnoDiario Turno(params FranjaProgramada[] franjas) =>
        new("Turno", franjas, "");

    // La union de los bloques cubre exactamente [inicioEsperado, finEsperado]: sin huecos ni solapes.
    private static void AsegurarCobertura(
        IReadOnlyList<BloqueTurno> bloques, DateTime inicioEsperado, DateTime finEsperado)
    {
        bloques.Should().NotBeEmpty();
        bloques[0].Inicio.Should().Be(inicioEsperado);
        bloques[^1].Fin.Should().Be(finEsperado);
        for (var i = 0; i < bloques.Count - 1; i++)
            bloques[i].Fin.Should().Be(bloques[i + 1].Inicio);
    }

    // Un bloque que termina exactamente a medianoche pertenece al dia de Inicio: no cruza.
    private static void AsegurarSinCruceDeMedianoche(IReadOnlyList<BloqueTurno> bloques) =>
        bloques.Should().OnlyContain(b => b.Inicio.Date == b.Fin.Date || b.Fin.TimeOfDay == TimeSpan.Zero);

    [Fact]
    public void Segmentar_RetornaUnBloqueOrdinaria_CuandoFranjaNoTieneDescansosNiExtras()
    {
        var franja = Franja(6, 0, 14, 0);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0)),
        };
        resultado.Should().Equal(esperado);
        AsegurarCobertura(resultado, new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0));
    }

    [Fact]
    public void Segmentar_RecortaLaOrdinariaEnTresBloques_CuandoHayUnDescansoContenido()
    {
        var descanso = SubFranja(10, 0, 11, 0);
        var franja = Franja(6, 0, 14, 0, descansos: [descanso]);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 10, 0, 0)),
            new BloqueTurno(TipoBloque.Descanso,
                new DateTime(2026, 8, 10, 10, 0, 0), new DateTime(2026, 8, 10, 11, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 11, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0)),
        };
        resultado.Should().Equal(esperado);
        AsegurarCobertura(resultado, new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0));
    }

    [Fact]
    public void Segmentar_RecortaLaOrdinariaEnTresBloques_CuandoHayUnaExtraContenida()
    {
        var extra = SubFranja(12, 0, 13, 0);
        var franja = Franja(6, 0, 14, 0, extras: [extra]);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 12, 0, 0)),
            new BloqueTurno(TipoBloque.Extra,
                new DateTime(2026, 8, 10, 12, 0, 0), new DateTime(2026, 8, 10, 13, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 13, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0)),
        };
        resultado.Should().Equal(esperado);
        AsegurarCobertura(resultado, new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0));
    }

    [Fact]
    public void Segmentar_IntercalaDescansoYExtraEnOrdenCronologico_CuandoLaFranjaTieneAmbos()
    {
        // La extra se declara antes que el descanso a proposito: el orden del resultado depende del
        // reloj, no de la coleccion.
        var extra = SubFranja(13, 0, 14, 0);
        var descanso = SubFranja(10, 0, 11, 0);
        var franja = Franja(6, 0, 16, 0, descansos: [descanso], extras: [extra]);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 10, 0, 0)),
            new BloqueTurno(TipoBloque.Descanso,
                new DateTime(2026, 8, 10, 10, 0, 0), new DateTime(2026, 8, 10, 11, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 11, 0, 0), new DateTime(2026, 8, 10, 13, 0, 0)),
            new BloqueTurno(TipoBloque.Extra,
                new DateTime(2026, 8, 10, 13, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 14, 0, 0), new DateTime(2026, 8, 10, 16, 0, 0)),
        };
        resultado.Should().Equal(esperado);
        AsegurarCobertura(resultado, new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 16, 0, 0));
    }

    [Fact]
    public void Segmentar_NoEmiteBloquesOrdinariosDeDuracionCero_CuandoLasSubFranjasTocanLosExtremosDeLaFranja()
    {
        // Sub-franjas en ambos extremos: no debe aparecer un bloque Ordinaria de duracion cero.
        var descanso = SubFranja(6, 0, 7, 0);
        var extra = SubFranja(13, 0, 14, 0);
        var franja = Franja(6, 0, 14, 0, descansos: [descanso], extras: [extra]);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Descanso,
                new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 7, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 7, 0, 0), new DateTime(2026, 8, 10, 13, 0, 0)),
            new BloqueTurno(TipoBloque.Extra,
                new DateTime(2026, 8, 10, 13, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0)),
        };
        resultado.Should().Equal(esperado);
        resultado.Should().OnlyContain(b => b.Fin > b.Inicio);
        AsegurarCobertura(resultado, new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0));
    }

    [Fact]
    public void Segmentar_RompeEnMedianoche_CuandoLaFranjaCruzaElLimiteDelDia()
    {
        var franja = Franja(22, 0, 6, 0, diaOffsetFin: 1);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 22, 0, 0), new DateTime(2026, 8, 11, 0, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 11, 0, 0, 0), new DateTime(2026, 8, 11, 6, 0, 0)),
        };
        resultado.Should().Equal(esperado);
        AsegurarCobertura(resultado, new DateTime(2026, 8, 10, 22, 0, 0), new DateTime(2026, 8, 11, 6, 0, 0));
        AsegurarSinCruceDeMedianoche(resultado);
    }

    [Fact]
    public void Segmentar_RompeElDescansoEnMedianoche_CuandoElDescansoCruzaElLimiteDelDiaDentroDeUnaFranjaNocturna()
    {
        // El propio descanso cruza la medianoche: ningun bloque, ordinario o de descanso, cruza el dia.
        var descanso = SubFranja(23, 0, 1, 0, diaOffsetFin: 1);
        var franja = Franja(22, 0, 8, 0, diaOffsetFin: 1, descansos: [descanso]);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 22, 0, 0), new DateTime(2026, 8, 10, 23, 0, 0)),
            new BloqueTurno(TipoBloque.Descanso,
                new DateTime(2026, 8, 10, 23, 0, 0), new DateTime(2026, 8, 11, 0, 0, 0)),
            new BloqueTurno(TipoBloque.Descanso,
                new DateTime(2026, 8, 11, 0, 0, 0), new DateTime(2026, 8, 11, 1, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 11, 1, 0, 0), new DateTime(2026, 8, 11, 8, 0, 0)),
        };
        resultado.Should().Equal(esperado);
        AsegurarCobertura(resultado, new DateTime(2026, 8, 10, 22, 0, 0), new DateTime(2026, 8, 11, 8, 0, 0));
        AsegurarSinCruceDeMedianoche(resultado);
    }

    [Fact]
    public void Segmentar_ConcatenaLosBloquesDeCadaFranjaEnOrden_CuandoElTurnoTieneVariasFranjasOrdinarias()
    {
        // Concatena los bloques de cada franja en el orden de FranjasOrdinarias, sin fusionarlas.
        var franjaManana = Franja(6, 0, 10, 0);
        var franjaTarde = Franja(14, 0, 18, 0);
        var turno = Turno(franjaManana, franjaTarde);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 10, 0, 0)),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 14, 0, 0), new DateTime(2026, 8, 10, 18, 0, 0)),
        };
        resultado.Should().Equal(esperado);
    }

    [Fact]
    public void Segmentar_EstampaLaSedeDeLaFranjaEnElBloqueOrdinario_CuandoLaFranjaTraeSede()
    {
        var sedeSuba = new SedeProgramada("SEDE-SUBA", "Suba");
        var franja = Franja(6, 0, 14, 0, sede: sedeSuba);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 6, 0, 0), new DateTime(2026, 8, 10, 14, 0, 0), sedeSuba),
        };
        resultado.Should().Equal(esperado);
    }

    [Fact]
    public void Segmentar_PropagaLaSedeDeLaFranjaATodosLosBloques_CuandoHayDescansoYExtra()
    {
        var sedeChapinero = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");
        var descanso = SubFranja(10, 0, 11, 0);
        var extra = SubFranja(12, 0, 13, 0);
        var franja = Franja(6, 0, 14, 0, descansos: [descanso], extras: [extra], sede: sedeChapinero);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        resultado.Should().HaveCount(5);
        resultado.Should().OnlyContain(b => b.Sede == sedeChapinero);
    }

    [Fact]
    public void Segmentar_DejaLaSedeEnNullEnTodosLosBloques_CuandoLaFranjaNoTraeSede()
    {
        var descanso = SubFranja(10, 0, 11, 0);
        var franja = Franja(6, 0, 14, 0, descansos: [descanso]);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        resultado.Should().OnlyContain(b => b.Sede == null);
    }

    [Fact]
    public void Segmentar_AsignaACadaBloqueLaSedeDeSuPropiaFranja_CuandoElTurnoTieneVariasFranjasConSedesDistintas()
    {
        var sedeSuba = new SedeProgramada("SEDE-SUBA", "Suba");
        var sedeChapinero = new SedeProgramada("SEDE-CHAPINERO", "Chapinero");
        var franjaManana = Franja(6, 0, 10, 0, sede: sedeSuba);
        var franjaTarde = Franja(14, 0, 18, 0, sede: sedeChapinero);
        var turno = Turno(franjaManana, franjaTarde);

        var resultado = turno.Segmentar(Fecha);

        resultado.Should().HaveCount(2);
        resultado[0].Sede.Should().Be(sedeSuba);
        resultado[1].Sede.Should().Be(sedeChapinero);
    }

    [Fact]
    public void Segmentar_ConservaLaSedeEnLosDosBloquesPartidos_CuandoLaFranjaConSedeCruzaMedianoche()
    {
        // La sede se estampa antes del corte en medianoche: es el unico test que delata perder la copia de
        // Sede en Tramo.RomperEnMedianoche.
        var sedeSuba = new SedeProgramada("SEDE-SUBA", "Suba");
        var franja = Franja(22, 0, 6, 0, diaOffsetFin: 1, sede: sedeSuba);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        var esperado = new[]
        {
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 10, 22, 0, 0), new DateTime(2026, 8, 11, 0, 0, 0), sedeSuba),
            new BloqueTurno(TipoBloque.Ordinaria,
                new DateTime(2026, 8, 11, 0, 0, 0), new DateTime(2026, 8, 11, 6, 0, 0), sedeSuba),
        };
        resultado.Should().Equal(esperado);
    }
}
