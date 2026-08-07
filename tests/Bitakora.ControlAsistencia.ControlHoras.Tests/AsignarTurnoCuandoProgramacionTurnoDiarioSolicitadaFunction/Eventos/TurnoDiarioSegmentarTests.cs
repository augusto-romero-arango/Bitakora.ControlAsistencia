// Issue #327: Segmentar el turno asignado en bloques absolutos de tiempo.
// TurnoDiario.Segmentar(DateOnly) es comportamiento de lectura pura sobre el payload de
// TurnoDiarioAsignado -- no hay comando ni evento nuevo (MEF-ADR-0012 Tell-don't-Ask: la
// aritmetica de resolucion de offsets de dia y ruptura en medianoche vive en el objeto que
// posee los datos, no como calculo externo). Estilo de test: Arrange/Act/Assert plano sobre un
// metodo puro -- no aplica el harness Given/When/Then de command handlers (mismo patron que
// IntervaloTemporalSegmentacionTests.cs).
//
// Semantica de los offsets confirmada contra el uso real en produccion (ClasificadorTrabajo,
// DepuradorDeMarcaciones): HoraInicio de la franja es siempre offset 0 (el dia de fecha); HoraFin
// usa DiaOffsetFin; cada SubFranjaProgramada (descanso/extra) usa su propio DiaOffsetInicio/Fin.
// Todos los offsets son relativos a la MISMA fecha ancla (la fecha de asignacion del turno), no al
// inicio de la franja individual.
//
// Issue #336 CA-3: Segmentar estampa en cada BloqueTurno la sede de su franja madre. Los
// descansos y extras NO tienen sede propia -- heredan la de la franja que los contiene (el
// glosario los define como contenidos en la ordinaria). Una franja sin sede asignada (turno
// prearmado multi-sede sin resolver) produce bloques con sede null.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;

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

    // CA-5: la union de los bloques cubre exactamente [inicioEsperado, finEsperado] -- el primer
    // bloque arranca donde arranca la franja, el ultimo termina donde termina, y cada bloque
    // intermedio termina exactamente donde empieza el siguiente (sin huecos, sin solapes).
    private static void AsegurarCobertura(
        IReadOnlyList<BloqueTurno> bloques, DateTime inicioEsperado, DateTime finEsperado)
    {
        bloques.Should().NotBeEmpty();
        bloques[0].Inicio.Should().Be(inicioEsperado);
        bloques[^1].Fin.Should().Be(finEsperado);
        for (var i = 0; i < bloques.Count - 1; i++)
            bloques[i].Fin.Should().Be(bloques[i + 1].Inicio);
    }

    // CA-4: ningun bloque cruza el limite del dia -- o queda contenido en un solo dia calendario,
    // o termina exactamente a medianoche (limite que pertenece al dia de Inicio, no lo cruza).
    private static void AsegurarSinCruceDeMedianoche(IReadOnlyList<BloqueTurno> bloques) =>
        bloques.Should().OnlyContain(b => b.Inicio.Date == b.Fin.Date || b.Fin.TimeOfDay == TimeSpan.Zero);

    [Fact]
    public void Segmentar_RetornaUnBloqueOrdinaria_CuandoFranjaNoTieneDescansosNiExtras()
    {
        // CA-1: franja simple 06:00-14:00, sin descansos ni extras -> un unico bloque Ordinaria.
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
        // CA-2: franja 06:00-14:00 con descanso 10:00-11:00 contenido ->
        // [Ordinaria 06:00-10:00, Descanso 10:00-11:00, Ordinaria 11:00-14:00], contiguos.
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
        // CA-3: mismo patron de recorte que CA-2, ahora con una extra (glosario: extras contenidas
        // en la ordinaria, igual que los descansos) 12:00-13:00 dentro de la franja 06:00-14:00.
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
        // CA-2 + CA-3 combinados: descansos y extras conviven en la misma franja y el recorte los
        // intercala por hora de inicio, no por la coleccion en que fueron declarados. Los datos
        // declaran la extra (13:00) ANTES que el descanso (10:00) a proposito: el orden del
        // resultado depende del reloj, no del orden de llegada.
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
        // CA-5 en su borde degenerado: cuando una sub-franja arranca exactamente donde arranca la
        // franja (06:00) y otra termina exactamente donde termina (14:00), no debe aparecer un
        // bloque Ordinaria de duracion cero en ninguno de los dos extremos.
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
        // CA-4: franja nocturna 22:00-06:00+1 (DiaOffsetFin=1) sin descansos ni extras -> se rompe
        // en las 00:00; el tramo posterior lleva la fecha del dia siguiente.
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
        // Caso borde de CA-4/CA-5 (notas tecnicas del issue): el propio descanso cruza la
        // medianoche dentro de una franja nocturna 22:00-08:00+1. Da igual el orden en que el
        // algoritmo aplique el recorte por descanso y la ruptura en 00:00: el resultado observable
        // es el mismo -> ningun bloque (ni ordinario ni de descanso) cruza el limite del dia.
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
        // Caso borde: turno partido (dos franjas independientes en el mismo dia, ej. jornada con
        // interrupcion larga) -- Segmentar concatena los bloques de cada franja en el orden en que
        // aparecen en FranjasOrdinarias, sin fusionarlas entre si.
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
        // CA-3: franja simple 06:00-14:00 con sede -> el unico bloque ordinario hereda esa sede.
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
        // CA-3: los descansos y extras no tienen sede propia -- heredan la de la franja madre que
        // los contiene (mismo criterio del glosario que ya aplica Segmentar para los tramos).
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
        // CA-3 (borde documentado en el issue): turno prearmado multi-sede asignado sin sede en la
        // solicitud -> la franja llega sin sede y esa ausencia se propaga tal cual a los bloques.
        var descanso = SubFranja(10, 0, 11, 0);
        var franja = Franja(6, 0, 14, 0, descansos: [descanso]);
        var turno = Turno(franja);

        var resultado = turno.Segmentar(Fecha);

        resultado.Should().OnlyContain(b => b.Sede == null);
    }

    [Fact]
    public void Segmentar_AsignaACadaBloqueLaSedeDeSuPropiaFranja_CuandoElTurnoTieneVariasFranjasConSedesDistintas()
    {
        // CA-3: turno partido con sedes distintas por franja (escenario que fija la semantica del
        // issue) -- cada bloque lleva la sede de SU franja madre, nunca una sede global del turno.
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
        // CA-3 x CA-4 (agregado en revision): la sede se estampa antes del corte en medianoche, asi
        // que el corte debe preservarla en AMBOS lados. Sin este test, romper la copia de Sede en
        // Tramo.RomperEnMedianoche dejaria sin sede a todo turno nocturno -- la mitad del dominio --
        // y ningun otro test lo delataria: los demas casos de sede no cruzan el limite del dia.
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
