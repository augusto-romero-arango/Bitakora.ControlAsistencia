// Issue #426: fase roja de la proyeccion AsistenciaDiaria (N1, SingleStreamProjection sobre el
// stream "dc:{CodigoColaborador}:{yyyyMMdd}" de DiaCalculadoAggregateRoot). Invocacion DIRECTA de
// los metodos estaticos de AsistenciaDiariaProjection -- no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event store): aqui se
// testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la clasificacion de Plan ni la derivacion de banderas bajo prueba para construir el valor
// esperado -- cada test declara el valor esperado como literal.
//
// HorasPorConcepto se verifica con BeEquivalentTo (no con el equality de record por defecto sobre
// AsistenciaDiaria completo): IReadOnlyDictionary<string, decimal> no tiene equality estructural
// generada por el compilador de records -- comparar la vista entera con Should().Be(...) compararia
// el diccionario por referencia y podria pasar o fallar por casualidad. Ver el mismo criterio ya
// documentado en HorasDiscriminadas.Equals (ControlHoras.DomainEvents).
//
// CA-1..CA-6 son variaciones del mismo eje (derivacion de Plan y del eje 2 de anomalias en
// Create/Apply); CA-7 (registro Async en el named store) vive en ConfiguracionMartenProjectionsTests.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class AsistenciaDiariaProjectionTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 8, 24);
    private const string StreamKey = "dc:EMP-001:20260824";

    private static ResumenColaborador ColaboradorDePrueba() =>
        new("CC-1098765432", CodigoColaborador, "Ana Ramirez");

    private static HorasDiscriminadas HorasDePrueba(IReadOnlyDictionary<string, decimal> horasPorConcepto) =>
        new(horasPorConcepto, []);

    private static DepuracionDiaRecibida CrearEvento(
        string? nombreTurno,
        IReadOnlyList<FranjaDepurada> franjas,
        IReadOnlyList<MarcacionDelDia> marcaciones,
        HorasDiscriminadas horas,
        ResumenColaborador? colaborador = null) =>
        new(StreamKey, CodigoColaborador, Fecha, colaborador ?? ColaboradorDePrueba(), nombreTurno, franjas,
            marcaciones, horas);

    private static FranjaDepurada FranjaValida() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 8, 24, 6, 0, 0), new DateTime(2026, 8, 24, 14, 0, 0), EsAnomala: false);

    private static FranjaDepurada FranjaAnomala() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        null, null, EsAnomala: true);

    private static MarcacionDelDia MarcacionDePrueba() =>
        new(new DateTime(2026, 8, 24, 6, 0, 0), "ENTRADA");

    // CA-1: jornada valida (NombreTurno + franjas >= 1) con marcaciones completas -> Create produce
    // la fila Provisional/ConJornada, HorasPorConcepto copiada tal cual, las cuatro banderas false.
    [Fact]
    public void Create_ProyectaJornadaValidaSinAnomalias_DesdeDepuracionDiaRecibida()
    {
        var horas = HorasDePrueba(new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });
        var evento = CrearEvento("Turno Manana", [FranjaValida()], [MarcacionDePrueba()], horas);

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Id.Should().Be(StreamKey);
        vista.CodigoColaborador.Should().Be(CodigoColaborador);
        vista.Fecha.Should().Be(Fecha);
        vista.Estado.Should().Be(EstadoAsistencia.Provisional);
        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.NombreTurno.Should().Be("Turno Manana");
        vista.NoSePresento.Should().BeFalse();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
        vista.HorasPorConcepto.Should().BeEquivalentTo(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });
    }

    // CA-2: jornada valida sin marcaciones -> NoSePresento true, el resto de banderas false.
    [Fact]
    public void Create_MarcaNoSePresento_CuandoJornadaValidaSinMarcaciones()
    {
        var horas = HorasDePrueba(new Dictionary<string, decimal>());
        var evento = CrearEvento("Turno Manana", [FranjaValida()], [], horas);

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.NoSePresento.Should().BeTrue();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
    }

    // CA-3: jornada valida con alguna franja EsAnomala -> FranjasIncompletas true.
    [Fact]
    public void Create_MarcaFranjasIncompletas_CuandoJornadaValidaConAlgunaFranjaAnomala()
    {
        var horas = HorasDePrueba(new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 4.00m });
        var evento = CrearEvento(
            "Turno Manana", [FranjaValida(), FranjaAnomala()], [MarcacionDePrueba()], horas);

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.FranjasIncompletas.Should().BeTrue();
        vista.NoSePresento.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
    }

    // CA-4 (primera mitad): descanso (NombreTurno + franjas vacias) con marcaciones -> VinoEnDescanso true.
    [Fact]
    public void Create_MarcaVinoEnDescanso_CuandoDescansoConMarcaciones()
    {
        var horas = HorasDePrueba(new Dictionary<string, decimal>());
        var evento = CrearEvento("Descanso", [], [MarcacionDePrueba()], horas);

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.Descanso);
        vista.NombreTurno.Should().Be("Descanso");
        vista.VinoEnDescanso.Should().BeTrue();
        vista.NoSePresento.Should().BeFalse();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
    }

    // CA-4 (segunda mitad): descanso sin marcaciones no es anomalia -- las cuatro banderas quedan false.
    [Fact]
    public void Create_DejaLasCuatroBanderasFalse_CuandoDescansoSinMarcaciones()
    {
        var horas = HorasDePrueba(new Dictionary<string, decimal>());
        var evento = CrearEvento("Descanso", [], [], horas);

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.Descanso);
        vista.NoSePresento.Should().BeFalse();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
    }

    // CA-5: sin programacion (NombreTurno null, franjas y horas vacias, marcaciones crudas) ->
    // Plan SinProgramar, TrabajoSinProgramacion true, HorasPorConcepto vacio.
    [Fact]
    public void Create_MarcaTrabajoSinProgramacion_CuandoSinProgramacionConMarcaciones()
    {
        var horas = HorasDePrueba(new Dictionary<string, decimal>());
        var evento = CrearEvento(null, [], [MarcacionDePrueba()], horas, colaborador: null);

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.SinProgramar);
        vista.NombreTurno.Should().BeNull();
        vista.TrabajoSinProgramacion.Should().BeTrue();
        vista.NoSePresento.Should().BeFalse();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.HorasPorConcepto.Should().BeEmpty();
    }

    // CA-6: una segunda foto sobre el mismo stream reemplaza Plan, NombreTurno, las cuatro banderas
    // y HorasPorConcepto ("el ultimo gana"); Estado sigue Provisional; Id/CodigoColaborador/Fecha
    // invariantes (identidad del stream, no viajan en el evento de Apply hacia otro valor).
    [Fact]
    public void Apply_ReemplazaPlanBanderasYHoras_CuandoLlegaUnaSegundaFoto()
    {
        var vistaPrevia = new AsistenciaDiaria(
            StreamKey, CodigoColaborador, Fecha, EstadoAsistencia.Provisional, PlanDelDia.ConJornada,
            "Turno Manana", NoSePresento: false, FranjasIncompletas: true, VinoEnDescanso: false,
            TrabajoSinProgramacion: false,
            HorasPorConcepto: new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 4.00m });

        // Segunda foto: la anomalia de la franja se corrigio y ahora la jornada quedo completa.
        var segundaHoras = HorasDePrueba(new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });
        var segundoEvento = CrearEvento(
            "Turno Manana", [FranjaValida()], [MarcacionDePrueba(), MarcacionDePrueba()], segundaHoras);

        var vista = AsistenciaDiariaProjection.Apply(segundoEvento, vistaPrevia);

        vista.Id.Should().Be(StreamKey);
        vista.CodigoColaborador.Should().Be(CodigoColaborador);
        vista.Fecha.Should().Be(Fecha);
        vista.Estado.Should().Be(EstadoAsistencia.Provisional);
        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.FranjasIncompletas.Should().BeFalse();
        vista.NoSePresento.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
        vista.HorasPorConcepto.Should().BeEquivalentTo(
            new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });
    }
}
