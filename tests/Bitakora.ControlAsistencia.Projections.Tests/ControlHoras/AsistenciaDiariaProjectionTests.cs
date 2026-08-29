// Los metodos de AsistenciaDiariaProjection son funciones puras evento -> vista: se invocan
// directamente, sin el DSL Given/When/Then de CommandHandlerTestBase (MEF-ADR-0002 lo reserva para
// command handlers contra el event store) y sin abrir ningun stream.
//
// Cada valor esperado se declara como literal (MEF-ADR-0002, no-tautologia): nunca se reusa la
// clasificacion de Plan ni la derivacion de banderas bajo prueba para construir el oraculo.
//
// HorasPorConcepto se verifica con BeEquivalentTo, nunca comparando la vista entera con
// Should().Be(...): IReadOnlyDictionary<string, decimal> no recibe equality estructural del
// compilador de records, asi que esa comparacion caeria en igualdad por referencia y pasaria o
// fallaria por casualidad (mismo motivo por el que HorasDiscriminadas escribe su Equals a mano).

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
// Alias, no nombre corto: issue #429 agrego ReadModels.ControlHoras.FranjaDepurada/MarcacionDelDia
// (tercer espejo del mismo termino, MEF-ADR-0039 decision 6), que colisionan (CS0104) con los
// homonimos de DomainEvents que este archivo ya usaba para construir DepuracionDiaRecibida.
using EventoFranjaDepurada = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.FranjaDepurada;
using EventoMarcacionDelDia = Bitakora.ControlAsistencia.ControlHoras.DomainEvents.MarcacionDelDia;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class AsistenciaDiariaProjectionTests
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 8, 24);
    private const string StreamKey = "dc:EMP-001:20260824";

    private static HorasDiscriminadas HorasDePrueba(IReadOnlyDictionary<string, decimal> horasPorConcepto) =>
        new(horasPorConcepto, []);

    private static HorasDiscriminadas SinHoras() => HorasDePrueba(new Dictionary<string, decimal>());

    private static DepuracionDiaRecibida CrearEvento(
        string? nombreTurno,
        IReadOnlyList<EventoFranjaDepurada> franjas,
        IReadOnlyList<EventoMarcacionDelDia> marcaciones,
        HorasDiscriminadas horas) =>
        new(StreamKey, CodigoColaborador, Fecha, new ResumenColaborador("CC-1098765432", CodigoColaborador,
            "Ana Ramirez"), nombreTurno, franjas, marcaciones, horas);

    private static EventoFranjaDepurada FranjaValida() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 8, 24, 6, 0, 0), new DateTime(2026, 8, 24, 14, 0, 0), EsAnomala: false);

    private static EventoFranjaDepurada FranjaAnomala() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        null, null, EsAnomala: true);

    private static EventoMarcacionDelDia MarcacionDePrueba() =>
        new(new DateTime(2026, 8, 24, 6, 0, 0), "ENTRADA");

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

    [Fact]
    public void Create_MarcaNoSePresento_CuandoJornadaValidaSinMarcaciones()
    {
        var evento = CrearEvento("Turno Manana", [FranjaValida()], [], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.ConJornada);
        vista.NoSePresento.Should().BeTrue();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
    }

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

    [Fact]
    public void Create_MarcaVinoEnDescanso_CuandoDescansoConMarcaciones()
    {
        var evento = CrearEvento("Descanso", [], [MarcacionDePrueba()], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.Descanso);
        vista.NombreTurno.Should().Be("Descanso");
        vista.VinoEnDescanso.Should().BeTrue();
        vista.NoSePresento.Should().BeFalse();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
    }

    [Fact]
    public void Create_DejaLasCuatroBanderasFalse_CuandoDescansoSinMarcaciones()
    {
        var evento = CrearEvento("Descanso", [], [], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.Descanso);
        vista.NoSePresento.Should().BeFalse();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.TrabajoSinProgramacion.Should().BeFalse();
    }

    [Fact]
    public void Create_MarcaTrabajoSinProgramacion_CuandoSinProgramacionConMarcaciones()
    {
        var evento = CrearEvento(null, [], [MarcacionDePrueba()], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.Plan.Should().Be(PlanDelDia.SinProgramar);
        vista.NombreTurno.Should().BeNull();
        vista.TrabajoSinProgramacion.Should().BeTrue();
        vista.NoSePresento.Should().BeFalse();
        vista.FranjasIncompletas.Should().BeFalse();
        vista.VinoEnDescanso.Should().BeFalse();
        vista.HorasPorConcepto.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ReemplazaPlanBanderasYHoras_CuandoLlegaUnaSegundaFoto()
    {
        var vistaPrevia = new AsistenciaDiaria(
            StreamKey, CodigoColaborador, Fecha, EstadoAsistencia.Provisional, PlanDelDia.ConJornada,
            "Turno Manana", NoSePresento: false, FranjasIncompletas: true, VinoEnDescanso: false,
            TrabajoSinProgramacion: false, ConflictoDeSedePendiente: false,
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

    // CA-1..CA-4 (issue #485): ConflictoDeSedePendiente, quinta bandera. Espejo simplificado de los
    // CAs de #482 -- misma regla de pertenencia (Timestamp == Entrada o Salida) que
    // DiaCalculadoAggregateRoot.DerivarSedeDeFranja, MEF-ADR-0018.

    [Fact]
    public void Create_MarcaConflictoDeSedePendiente_CuandoSedeProgramadaYSedeMarcadaDifierenEnUnaFranja()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = new EventoFranjaDepurada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, EsAnomala: false,
            CodigoSedeProgramada: "SEDE-X");
        var marcacion = new EventoMarcacionDelDia(entrada, "ENTRADA", CodigoSede: "SEDE-Y");
        var evento = CrearEvento("Turno Manana", [franja], [marcacion], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.ConflictoDeSedePendiente.Should().BeTrue();
    }

    [Fact]
    public void Create_DejaConflictoDeSedePendienteEnFalse_CuandoSoloHayUnaFuenteDeSedeEnLaFranja()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = new EventoFranjaDepurada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, EsAnomala: false,
            CodigoSedeProgramada: "SEDE-X");
        var marcacion = new EventoMarcacionDelDia(entrada, "ENTRADA");
        var evento = CrearEvento("Turno Manana", [franja], [marcacion], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.ConflictoDeSedePendiente.Should().BeFalse();
    }

    // Espejo del CA-5 de #482: la misma sede con CC distinto entre fuentes NO es conflicto.
    [Fact]
    public void Create_DejaConflictoDeSedePendienteEnFalse_CuandoLaSedeCoincideYSoloElCentroDeCostosDifiereEntreFuentes()
    {
        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franja = new EventoFranjaDepurada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, EsAnomala: false,
            CodigoSedeProgramada: "SEDE-X", CentroDeCostosProgramado: "CC-1");
        var marcacion = new EventoMarcacionDelDia(
            entrada, "ENTRADA", CodigoSede: "SEDE-X", CentroDeCostos: "CC-2");
        var evento = CrearEvento("Turno Manana", [franja], [marcacion], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.ConflictoDeSedePendiente.Should().BeFalse();
    }

    // CA-3: foto pre-#484 -- franjas y marcaciones sin campos de sede (FranjaValida/MarcacionDePrueba
    // ya los dejan en null por defecto) -- nunca conflicto falso.
    [Fact]
    public void Create_DejaConflictoDeSedePendienteEnFalse_CuandoFranjasYMarcacionesNoTraenCamposDeSede()
    {
        var evento = CrearEvento("Turno Manana", [FranjaValida()], [MarcacionDePrueba()], SinHoras());

        var vista = AsistenciaDiariaProjection.Create(evento);

        vista.ConflictoDeSedePendiente.Should().BeFalse();
    }

    // CA-4: "el ultimo gana" -- espejo del CA-6 de #482.
    [Fact]
    public void Apply_ApagaConflictoDeSedePendiente_CuandoLaFotoNuevaYaNoTraeLaDiscrepanciaDeSede()
    {
        var vistaPrevia = new AsistenciaDiaria(
            StreamKey, CodigoColaborador, Fecha, EstadoAsistencia.Provisional, PlanDelDia.ConJornada,
            "Turno Manana", NoSePresento: false, FranjasIncompletas: false, VinoEnDescanso: false,
            TrabajoSinProgramacion: false, ConflictoDeSedePendiente: true,
            HorasPorConcepto: new Dictionary<string, decimal>());

        var entrada = new DateTime(2026, 8, 24, 6, 0, 0);
        var salida = new DateTime(2026, 8, 24, 14, 0, 0);
        var franjaSinDiscrepancia = new EventoFranjaDepurada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0, entrada, salida, EsAnomala: false,
            CodigoSedeProgramada: "SEDE-X");
        var marcacion = new EventoMarcacionDelDia(entrada, "ENTRADA", CodigoSede: "SEDE-X");
        var segundoEvento = CrearEvento(
            "Turno Manana", [franjaSinDiscrepancia], [marcacion], SinHoras());

        var vista = AsistenciaDiariaProjection.Apply(segundoEvento, vistaPrevia);

        vista.ConflictoDeSedePendiente.Should().BeFalse();
    }
}
