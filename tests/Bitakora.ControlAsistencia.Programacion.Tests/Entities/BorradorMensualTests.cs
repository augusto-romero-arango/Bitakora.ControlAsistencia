using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class BorradorMensualTests
{
    private static readonly ColaboradorProgramado Colaborador = new("CC-12345678", "E001", "Juan Perez");

    private static readonly TurnoProgramado Turno = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly JornadaProgramada Jornada = new(
        Guid.Parse("018e4c1a-4f2b-7000-8000-00000000861a"),
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1));

    private static ProgramacionTurnoAgregadaAlBorrador Agregada(Guid solicitudId, params DateOnly[] fechas) =>
        new(solicitudId, Colaborador, fechas, Turno, null, Jornada);

    [Fact]
    public void ComputarStreamId_ArmaPrefijoBmCodigoYMes()
    {
        BorradorMensual.ComputarStreamId("E001", new DateOnly(2026, 10, 28)).Should().Be("bm:E001:202610");
    }

    [Fact]
    public void ComputarStreamId_SeDivideEnTresPartesPorDosPuntos()
    {
        BorradorMensual.ComputarStreamId("E001", new DateOnly(2026, 11, 3)).Split(':').Should().HaveCount(3);
    }

    [Fact]
    public void ComputarStreamId_RellenaElMesConCero_CuandoElMesTieneUnDigito()
    {
        BorradorMensual.ComputarStreamId("E001", new DateOnly(2027, 5, 10)).Should().Be("bm:E001:202705");
    }

    [Fact]
    public void Apply_AgregaLasFechasAlBorrador_CuandoSeProgramaUnTurno()
    {
        var borrador = new BorradorMensual();

        borrador.Apply(Agregada(Guid.NewGuid(), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6)));

        borrador.FechasEnBorrador.Should().Equal(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6));
    }

    [Fact]
    public void Apply_NoDuplicaLaFecha_CuandoUnaSegundaSolicitudLaSobrescribe()
    {
        var borrador = new BorradorMensual();

        borrador.Apply(Agregada(Guid.NewGuid(), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6)));
        borrador.Apply(Agregada(Guid.NewGuid(), new DateOnly(2026, 10, 6)));

        borrador.FechasEnBorrador.Should().BeEquivalentTo(new[] { new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6) });
        borrador.FechasEnBorrador.Should().HaveCount(2);
    }

    [Fact]
    public void Apply_QuitaLasFechasDelBorrador_CuandoSePublican()
    {
        var borrador = new BorradorMensual();
        borrador.Apply(Agregada(Guid.NewGuid(), new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6)));

        borrador.Apply(new ProgramacionPublicada(Colaborador, [new DateOnly(2026, 10, 5)]));

        borrador.FechasEnBorrador.Should().Equal(new DateOnly(2026, 10, 6));
    }

    [Fact]
    public void Publicar_DevuelveLotesVacios_CuandoElBorradorNoTieneFechasEnElRango()
    {
        var borrador = new BorradorMensual();
        borrador.Apply(Agregada(Guid.NewGuid(), new DateOnly(2026, 10, 5)));

        var resultado = borrador.Publicar(new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 20), null);

        resultado.Lotes.Should().BeEmpty();
        borrador.FechasEnBorrador.Should().Equal(new DateOnly(2026, 10, 5));
    }
}
