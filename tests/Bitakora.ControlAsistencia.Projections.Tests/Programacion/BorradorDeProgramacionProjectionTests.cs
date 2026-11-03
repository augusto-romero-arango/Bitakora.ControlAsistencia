// Invocacion DIRECTA de BorradorDeProgramacionProjection (N2, MEF-ADR-0035): funciones puras
// evento -> vista. Oraculos armados a mano (MEF-ADR-0002). Las vistas llevan colecciones sin
// igualdad por valor: se compara con BeEquivalentTo / Equal sobre elementos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Projections.Programacion;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using BloqueVista = Bitakora.ControlAsistencia.ReadModels.Programacion.Bloque;
using TipoBloqueVista = Bitakora.ControlAsistencia.ReadModels.Programacion.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.Tests.Programacion;

public class BorradorDeProgramacionProjectionTests
{
    private static ColaboradorProgramado Ana() => new("CC-1098765432", "EMP-001", "Ana Ramirez");

    private static DateOnly Oct(int dia) => new(2026, 10, dia);

    private static DateOnly Nov(int dia) => new(2026, 11, dia);

    private static TurnoProgramado Manana(SedeProgramada? sedeFranja = null) =>
        new("Turno Manana",
            [new FranjaProgramada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)", sedeFranja)],
            "Turno Manana: (06:00-14:00)");

    private static TurnoProgramado Noche() =>
        new("Turno Noche",
            [new FranjaProgramada(new TimeOnly(22, 0), new TimeOnly(6, 0), 1, [], [], "(22:00-06:00)")],
            "Turno Noche: (22:00-06:00)");

    private static JornadaProgramada Jornada() =>
        new(Guid.Parse("019600a0-0000-7000-8000-000000000001"), LimitesJornadaDePrueba());

    private static LimitesJornada LimitesJornadaDePrueba() => LimitesJornada.Crear(
        HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(10, 0), HorasYMinutos.Crear(0, 0), 1);

    private static ProgramacionTurnoAgregadaAlBorrador Agregar(
        TurnoProgramado turno, SedeProgramada? sede, params DateOnly[] fechas) =>
        new(Guid.NewGuid(), Ana(), fechas, turno, sede, Jornada());

    private static DiaDeBorrador DiaManana(DateOnly fecha, string? sedeId = null, string? sede = null) =>
        new(fecha, "Turno Manana", "Turno Manana: (06:00-14:00)",
            [new BloqueVista(TipoBloqueVista.Ordinaria, fecha.ToDateTime(new TimeOnly(6, 0)),
                fecha.ToDateTime(new TimeOnly(14, 0)), sedeId, sede)],
            false);

    private static BorradorDeProgramacion Vista(params DiaDeBorrador[] dias) =>
        new("EMP-001", "EMP-001", "CC-1098765432", "Ana Ramirez", dias);

    // CA-1
    [Fact]
    public void Create_ProyectaElBorradorConUnaCasillaPorFecha_DesdeProgramacionTurnoAgregadaAlBorrador()
    {
        var vista = BorradorDeProgramacionProjection.Create(Agregar(Manana(), null, Oct(13), Oct(14)));

        vista.Should().BeEquivalentTo(Vista(DiaManana(Oct(13)), DiaManana(Oct(14))),
            o => o.WithStrictOrdering());
    }

    // CA-1: la franja que trae sede la conserva en el bloque.
    [Fact]
    public void Create_ConservaLaSedeDeLaFranja_CuandoLaFranjaLaTrae()
    {
        var sede = new SedeProgramada("sede-1", "Sede Norte");

        var vista = BorradorDeProgramacionProjection.Create(Agregar(Manana(sede), null, Oct(13)));

        vista.Dias.Single().Bloques.Single().Should().Be(
            new BloqueVista(TipoBloqueVista.Ordinaria, Oct(13).ToDateTime(new TimeOnly(6, 0)),
                Oct(13).ToDateTime(new TimeOnly(14, 0)), "sede-1", "Sede Norte"));
    }

    // CA-1: la sede de programacion rellena la franja sin sede (ConSedePorDefecto).
    [Fact]
    public void Create_UsaLaSedeDeProgramacion_CuandoLaFranjaNoTraeSede()
    {
        var sede = new SedeProgramada("sede-2", "Sede Sur");

        var vista = BorradorDeProgramacionProjection.Create(Agregar(Manana(), sede, Oct(13)));

        vista.Dias.Single().Bloques.Single().Should().Be(
            new BloqueVista(TipoBloqueVista.Ordinaria, Oct(13).ToDateTime(new TimeOnly(6, 0)),
                Oct(13).ToDateTime(new TimeOnly(14, 0)), "sede-2", "Sede Sur"));
    }

    // CA-1: la franja que cruza medianoche termina al dia siguiente.
    [Fact]
    public void Create_TerminaElBloqueAlDiaSiguiente_CuandoLaFranjaCruzaMedianoche()
    {
        var vista = BorradorDeProgramacionProjection.Create(Agregar(Noche(), null, Oct(13)));

        vista.Dias.Single().Bloques.Single().Should().Be(
            new BloqueVista(TipoBloqueVista.Ordinaria, Oct(13).ToDateTime(new TimeOnly(22, 0)),
                Oct(14).ToDateTime(new TimeOnly(6, 0))));
    }

    // CA-1: el descanso parte la franja en bloques ordinarios alrededor suyo, con la misma sede.
    [Fact]
    public void Create_ParteLaFranjaEnBloques_CuandoLaFranjaTraeUnDescanso()
    {
        var sede = new SedeProgramada("sede-1", "Sede Norte");
        var conDescanso = new TurnoProgramado("Turno Manana",
            [new FranjaProgramada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 30), 0, 0, "(10:00-10:30)")],
                [], "(06:00-14:00)", sede)],
            "Turno Manana: (06:00-14:00)");

        var vista = BorradorDeProgramacionProjection.Create(Agregar(conDescanso, null, Oct(13)));

        vista.Dias.Single().Bloques.Should().Equal(
            new BloqueVista(TipoBloqueVista.Ordinaria, Oct(13).ToDateTime(new TimeOnly(6, 0)),
                Oct(13).ToDateTime(new TimeOnly(10, 0)), "sede-1", "Sede Norte"),
            new BloqueVista(TipoBloqueVista.Descanso, Oct(13).ToDateTime(new TimeOnly(10, 0)),
                Oct(13).ToDateTime(new TimeOnly(10, 30)), "sede-1", "Sede Norte"),
            new BloqueVista(TipoBloqueVista.Ordinaria, Oct(13).ToDateTime(new TimeOnly(10, 30)),
                Oct(13).ToDateTime(new TimeOnly(14, 0)), "sede-1", "Sede Norte"));
    }

    [Fact]
    public void Create_MarcaLaCasillaComoHorarioAbierto_CuandoElTurnoEsDeHorarioAbierto()
    {
        var abierto = new TurnoProgramado("Turno Abierto", [], "Turno Abierto: 8 h", 0, true);

        var vista = BorradorDeProgramacionProjection.Create(Agregar(abierto, null, Oct(13)));

        vista.Dias.Single().Should().BeEquivalentTo(
            new DiaDeBorrador(Oct(13), "Turno Abierto", "Turno Abierto: 8 h", [], true));
    }

    // CA-1: el ultimo gana -- una fecha reprogramada reemplaza su casilla y conserva las demas.
    [Fact]
    public void Apply_ReemplazaLaCasillaDeLaFecha_CuandoSeVuelveAProgramar()
    {
        var previa = Vista(DiaManana(Oct(13)), DiaManana(Oct(14)));

        var vista = BorradorDeProgramacionProjection.Apply(Agregar(Noche(), null, Oct(14)), previa);

        vista.Dias.Select(d => (d.Fecha, d.NombreTurno)).Should().Equal(
            (Oct(13), "Turno Manana"), (Oct(14), "Turno Noche"));
    }

    [Fact]
    public void Apply_AgregaLasFechasNuevas_CuandoNoEstabanEnElBorrador()
    {
        var previa = Vista(DiaManana(Oct(13)));

        var vista = BorradorDeProgramacionProjection.Apply(Agregar(Manana(), null, Oct(15)), previa);

        vista.Dias.Select(d => d.Fecha).Should().Equal(Oct(13), Oct(15));
    }

    // CA-2
    [Fact]
    public void Apply_QuitaSoloLasFechasPublicadas_CuandoProgramacionPublicada()
    {
        var previa = Vista(DiaManana(Oct(13)), DiaManana(Oct(14)), DiaManana(Oct(15)));

        var vista = BorradorDeProgramacionProjection.Apply(
            new ProgramacionPublicada(Ana(), [Oct(13), Oct(15)]), previa);

        vista!.Dias.Select(d => d.Fecha).Should().Equal(Oct(14));
    }

    [Fact]
    public void Apply_BorraElDocumento_CuandoPublicarVaciaElBorrador()
    {
        var previa = Vista(DiaManana(Oct(13)));

        var vista = BorradorDeProgramacionProjection.Apply(new ProgramacionPublicada(Ana(), [Oct(13)]), previa);

        vista.Should().BeNull();
    }

    [Fact]
    public void Apply_QuitaSoloLasFechasDescartadas_CuandoBorradorDeProgramacionDescartado()
    {
        var previa = Vista(DiaManana(Oct(13)), DiaManana(Oct(14)));

        var vista = BorradorDeProgramacionProjection.Apply(
            new BorradorDeProgramacionDescartado(Ana(), [Oct(14)]), previa);

        vista!.Dias.Select(d => d.Fecha).Should().Equal(Oct(13));
    }

    [Fact]
    public void Apply_BorraElDocumento_CuandoDescartarVaciaElBorrador()
    {
        var previa = Vista(DiaManana(Oct(13)), DiaManana(Oct(14)));

        var vista = BorradorDeProgramacionProjection.Apply(
            new BorradorDeProgramacionDescartado(Ana(), [Oct(13), Oct(14)]), previa);

        vista.Should().BeNull();
    }

    [Fact]
    public void Apply_IgnoraLasFechasAjenas_CuandoSePublicaUnaFechaQueNoEstaEnElBorrador()
    {
        var previa = Vista(DiaManana(Oct(13)));

        var vista = BorradorDeProgramacionProjection.Apply(new ProgramacionPublicada(Ana(), [Oct(20)]), previa);

        vista!.Dias.Select(d => d.Fecha).Should().Equal(Oct(13));
    }

    // CA-3: octubre y noviembre (dos streams bm:) alimentan un solo documento; publicar octubre
    // deja solo noviembre.
    [Fact]
    public void Apply_UneLosDosMeses_YPublicarOctubreDejaSoloNoviembre()
    {
        var octubre = BorradorDeProgramacionProjection.Create(Agregar(Manana(), null, Oct(30), Oct(31)));

        var conNoviembre = BorradorDeProgramacionProjection.Apply(Agregar(Manana(), null, Nov(2)), octubre);
        conNoviembre.Dias.Select(d => d.Fecha).Should().Equal(Oct(30), Oct(31), Nov(2));

        var tras = BorradorDeProgramacionProjection.Apply(
            new ProgramacionPublicada(Ana(), [Oct(30), Oct(31)]), conNoviembre);

        tras!.Dias.Select(d => d.Fecha).Should().Equal(Nov(2));
    }
}
