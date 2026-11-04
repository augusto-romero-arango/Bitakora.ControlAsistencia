using Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction;
using Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.DescartarBorradorDeProgramacionFunction;

public class DescartarBorradorDeProgramacionCommandHandlerTests : CommandHandlerAsyncTest<DescartarBorradorDeProgramacion>
{
    private const string Codigo = "E001";
    private const string StreamOctubre = "bm:E001:202610";
    private const string StreamNoviembre = "bm:E001:202611";
    private const string StreamAusencias = "ac:E001";

    private static readonly Guid Solicitud1 = Guid.Parse("019600a0-0000-7000-8000-000000001405");
    private static readonly Guid AusenciaId = Guid.Parse("019600a0-0000-7000-8000-000000001407");
    private static readonly Guid JornadaId = Guid.Parse("018e4c1a-4f2b-7000-8000-00000000861a");

    private static readonly ColaboradorProgramado Colaborador = new("CC-12345678", Codigo, "Juan Perez");

    private static readonly TurnoProgramado TurnoManana = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "06:00-14:00")
        }.AsReadOnly(),
        "06:00-14:00");

    private static readonly JornadaProgramada Jornada = new(
        JornadaId,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1));

    protected override ICommandHandlerAsync<DescartarBorradorDeProgramacion> Handler =>
        new DescartarBorradorDeProgramacionCommandHandler(EventStore);

    private static DateOnly Oct(int dia) => new(2026, 10, dia);
    private static DateOnly Nov(int dia) => new(2026, 11, dia);

    private static DescartarBorradorDeProgramacion Comando(DateOnly desde, DateOnly hasta) => new(Codigo, desde, hasta);

    private static ProgramacionTurnoAgregadaAlBorrador Agregada(params DateOnly[] fechas) =>
        new(Solicitud1, Colaborador, fechas, TurnoManana, null, Jornada);

    private static BorradorDeProgramacionDescartado Descartado(params DateOnly[] fechas) => new(Colaborador, fechas);

    private static int EnBorrador(BorradorMensual b) => b.FechasEnBorrador.Count;

    [Fact]
    public async Task DescartarBorradorDeProgramacion_EmiteBorradorDescartado_CuandoElRangoCubreElBorrador()
    {
        Given(StreamOctubre, Agregada(Oct(5), Oct(6), Oct(7)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Descartado(Oct(5), Oct(6), Oct(7)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_DescartaSoloLasFechasDelRango_CuandoElBorradorTieneFechasFuera()
    {
        Given(StreamOctubre, Agregada(Oct(5), Oct(6), Oct(7), Oct(8)));

        await WhenAsync(Comando(Oct(6), Oct(7)));

        Then(StreamOctubre, Descartado(Oct(6), Oct(7)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 2);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_EmiteUnEventoPorMes_CuandoElRangoCruzaMes()
    {
        Given(StreamOctubre, Agregada(Oct(28), Oct(29), Oct(30), Oct(31)));
        Given(StreamNoviembre, Agregada(Nov(1), Nov(2), Nov(3)));

        await WhenAsync(Comando(Oct(28), Nov(3)));

        Then(StreamOctubre, Descartado(Oct(28), Oct(29), Oct(30), Oct(31)));
        Then(StreamNoviembre, Descartado(Nov(1), Nov(2), Nov(3)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
        And<BorradorMensual, int>(StreamNoviembre, EnBorrador, 0);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_DescartaLaFechaConAusencia_CuandoLaAusenciaEstaVigente()
    {
        Given(StreamOctubre, Agregada(Oct(5), Oct(6), Oct(7)));
        Given(StreamAusencias,
            new AusenciaProgramada(AusenciaId, Colaborador, Oct(6), Oct(6), MotivoAusencia.Vacaciones));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre, Descartado(Oct(5), Oct(6), Oct(7)));
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_NoEmite_CuandoNingunMesDelRangoTieneBorrador()
    {
        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_NoEmite_CuandoElBorradorNoTieneFechasEnElRango()
    {
        Given(StreamOctubre, Agregada(Oct(5), Oct(6)));

        await WhenAsync(Comando(Oct(10), Oct(20)));

        Then(StreamOctubre);
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 2);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_NoEmite_CuandoYaSeDescartoLoDelRango()
    {
        Given(StreamOctubre, Agregada(Oct(5), Oct(6)), Descartado(Oct(5), Oct(6)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamOctubre);
        And<BorradorMensual, int>(StreamOctubre, EnBorrador, 0);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_NoTocaElBorradorDeOtroMes_CuandoElRangoNoLoCubre()
    {
        Given(StreamNoviembre, Agregada(Nov(1), Nov(2), Nov(3)));

        await WhenAsync(Comando(Oct(1), Oct(31)));

        Then(StreamNoviembre);
        And<BorradorMensual, int>(StreamNoviembre, EnBorrador, 3);
    }
}
