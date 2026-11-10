// Toda entrega de DiaDepurado se persiste, sin comparar contra el estado previo ni deduplicar
// (decision de sesion 2026-08-24), incluida la del dia sin jornada valida.

using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using EventoBus = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using ColaboradorBus = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado;

public class DiaDepuradoEventHandlerTests : PrivateEventHandlerAsyncTest<EventoBus.DiaDepurado>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"dc:{CodigoColaborador}:20260315";

    protected override IPrivateEventHandlerAsync<EventoBus.DiaDepurado> Handler =>
        new DiaDepuradoEventHandler(EventStore);

    // Entrada: DiaDepurado tal como lo publica el productor (PrivateEvents).

    private static readonly ColaboradorBus ColaboradorRecibido =
        new("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    private static readonly EventoBus.FranjaDepurada FranjaRecibida = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false);

    private static readonly EventoBus.MarcacionDelDia MarcacionRecibida =
        new(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA");

    private static EventoBus.HorasDiscriminadas HorasRecibidas() => new(
        new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }, []);

    private static EventoBus.DiaDepurado CrearDiaDepurado(
        ColaboradorBus? colaborador,
        string? nombreTurno,
        IReadOnlyList<EventoBus.FranjaDepurada> franjas,
        IReadOnlyList<EventoBus.MarcacionDelDia> marcaciones,
        EventoBus.HorasDiscriminadas horas) =>
        new(CodigoColaborador, Fecha, colaborador, nombreTurno, franjas, marcaciones, horas);

    // Oraculo independiente: el evento persistido esperado se arma a mano con los tipos de
    // ControlHoras.DomainEvents, sin reusar el mapeo del handler bajo prueba.

    private static ResumenColaborador ColaboradorEsperado() =>
        new("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    private static FranjaDepurada FranjaEsperada() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false);

    private static MarcacionDelDia MarcacionEsperada() =>
        new(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA");

    private static HorasDiscriminadas HorasEsperadas() => new(
        new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }, []);

    // Dia nuevo: crea el stream dc:{codigo}:{yyyyMMdd} con la foto completa y el dia queda Provisional.
    [Fact]
    public async Task DiaDepurado_CreaDiaCalculadoProvisional_CuandoElDiaEsNuevo()
    {
        // Sin Given - el stream dc:EMP-001:20260315 no existe
        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Manana", [FranjaRecibida], [MarcacionRecibida], HorasRecibidas()));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperada()], [MarcacionEsperada()], HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // Dia existente: los valores provisionales son los de la ultima foto (la previa no tenia turno ni
    // franjas).
    [Fact]
    public async Task DiaDepurado_AgregaOtraDepuracionAlMismoStream_CuandoElDiaYaExiste()
    {
        var fotoPrevia = new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [MarcacionEsperada()],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []));
        Given(StreamId, fotoPrevia);

        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Manana", [FranjaRecibida], [MarcacionRecibida], HorasRecibidas()));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperada()], [MarcacionEsperada()], HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // Colaborador/NombreTurno null, franjas y horas vacias, marcaciones crudas.
    [Fact]
    public async Task DiaDepurado_CreaDiaCalculadoProvisional_CuandoElDiaNoTieneJornadaValida()
    {
        await WhenAsync(CrearDiaDepurado(
            null, null, [], [MarcacionRecibida],
            new EventoBus.HorasDiscriminadas(new Dictionary<string, decimal>(), [])));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [MarcacionEsperada()],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // Sin dedup. Then solo ve el evento NUEVO de este WhenAsync (el Given no cuenta).
    [Fact]
    public async Task DiaDepurado_EmiteUnNuevoEvento_CuandoLlegaLaMismaFotoQueYaExisteEnElStream()
    {
        var fotoIdentica = new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperada()], [MarcacionEsperada()], HorasEsperadas());
        Given(StreamId, fotoIdentica);

        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Manana", [FranjaRecibida], [MarcacionRecibida], HorasRecibidas()));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperada()], [MarcacionEsperada()], HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // Sede programada (plan, por franja) y sede marcada (realidad, por marcacion).

    private static readonly EventoBus.FranjaDepurada FranjaConSedeProgramadaRecibida = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false,
        "SEDE-01", "Sede Principal", "CC-100");

    private static readonly EventoBus.FranjaDepurada FranjaTardeSinSedeRecibida = new(
        new TimeOnly(14, 0), new TimeOnly(22, 0), 0,
        new DateTime(2026, 3, 15, 14, 0, 0), new DateTime(2026, 3, 15, 22, 0, 0), false);

    private static readonly EventoBus.MarcacionDelDia MarcacionConSedeMarcadaRecibida = new(
        new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA",
        "SEDE-02", "Sede Norte", "CC-200");

    private static readonly EventoBus.MarcacionDelDia MarcacionSalidaSinSedeRecibida =
        new(new DateTime(2026, 3, 15, 14, 0, 0), "SALIDA");

    private static FranjaDepurada FranjaEsperadaConSedeProgramada() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false,
        "SEDE-01", "Sede Principal", "CC-100");

    private static FranjaDepurada FranjaTardeEsperadaSinSede() => new(
        new TimeOnly(14, 0), new TimeOnly(22, 0), 0,
        new DateTime(2026, 3, 15, 14, 0, 0), new DateTime(2026, 3, 15, 22, 0, 0), false);

    private static MarcacionDelDia MarcacionEsperadaConSedeMarcada() => new(
        new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA",
        "SEDE-02", "Sede Norte", "CC-200");

    private static MarcacionDelDia MarcacionSalidaEsperadaSinSede() =>
        new(new DateTime(2026, 3, 15, 14, 0, 0), "SALIDA");

    // La marcacion sigue sin sede: el campo opuesto no se contamina.
    [Fact]
    public async Task DiaDepurado_PersisteLaSedeProgramadaDeLaFranja_CuandoLaFranjaTraeSede()
    {
        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Manana",
            [FranjaConSedeProgramadaRecibida], [MarcacionRecibida], HorasRecibidas()));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperadaConSedeProgramada()], [MarcacionEsperada()], HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // La franja sigue sin sede programada: el campo opuesto no se contamina.
    [Fact]
    public async Task DiaDepurado_PersisteLaSedeMarcadaDeLaMarcacion_CuandoLaMarcacionTraeSedeEstampada()
    {
        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Manana",
            [FranjaRecibida], [MarcacionConSedeMarcadaRecibida], HorasRecibidas()));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperada()], [MarcacionEsperadaConSedeMarcada()], HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // El mapeo es por elemento y no propaga la sede del primero al resto: entrada y salida de una misma
    // franja pueden venir de dispositivos de sedes distintas.
    [Fact]
    public async Task DiaDepurado_ConservaLaSedeDeCadaElemento_CuandoConvivenFranjasYMarcacionesConYSinSede()
    {
        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Partido",
            [FranjaConSedeProgramadaRecibida, FranjaTardeSinSedeRecibida],
            [MarcacionConSedeMarcadaRecibida, MarcacionSalidaSinSedeRecibida],
            HorasRecibidas()));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Partido",
            [FranjaEsperadaConSedeProgramada(), FranjaTardeEsperadaSinSede()],
            [MarcacionEsperadaConSedeMarcada(), MarcacionSalidaEsperadaSinSede()],
            HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // Un dia Aprobado no ignora la foto tardia: la persiste completa como evidencia en el mismo stream.
    [Fact]
    public async Task DiaDepurado_PersisteDepuracionPosAprobacionRecibida_CuandoElDiaYaEstaAprobado()
    {
        Given(StreamId,
            new DepuracionDiaRecibida(
                StreamId, CodigoColaborador, Fecha, null, null, [], [],
                new HorasDiscriminadas(new Dictionary<string, decimal>(), [])),
            DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));

        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Manana", [FranjaRecibida], [MarcacionRecibida], HorasRecibidas()));

        Then(StreamId, new DepuracionPosAprobacionRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperada()], [MarcacionEsperada()], HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    [Fact]
    public async Task DiaDepurado_PersisteElMotivoDeAusencia_CuandoElDiaTraeMotivo()
    {
        await WhenAsync(new EventoBus.DiaDepurado(
            CodigoColaborador, Fecha, null, null, [], [MarcacionRecibida],
            new EventoBus.HorasDiscriminadas(new Dictionary<string, decimal>(), []), "Vacaciones"));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [MarcacionEsperada()],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), "Vacaciones"));
        And<DiaCalculadoAggregateRoot, string>(
            StreamId, d => d.GenerarDepuracionDelDia().MotivoAusencia!, "Vacaciones");
    }

    // Un DiaDepurado sin motivo tras una ausencia es su cancelacion.
    [Fact]
    public async Task DiaDepurado_DevuelveElPlanDelTurno_CuandoLlegaSinMotivoTrasUnaAusencia()
    {
        Given(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, null, [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), "Vacaciones"));

        await WhenAsync(CrearDiaDepurado(
            ColaboradorRecibido, "Turno Manana", [FranjaRecibida], [MarcacionRecibida], HorasRecibidas()));

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, ColaboradorEsperado(), "Turno Manana",
            [FranjaEsperada()], [MarcacionEsperada()], HorasEsperadas()));
        And<DiaCalculadoAggregateRoot, ReadModels.ControlHoras.PlanDelDia>(
            StreamId, d => d.GenerarDepuracionDelDia().Plan, ReadModels.ControlHoras.PlanDelDia.ConJornada);
    }

    [Fact]
    public async Task DiaDepurado_ConservaLaPertenenciaDeCadaMarcacion_CuandoElBusLaTrae()
    {
        var recibido = CrearDiaDepurado(null, "Turno Manana", [],
            [
                new EventoBus.MarcacionDelDia(new DateTime(2026, 3, 15, 4, 30, 0), "ENTRADA", Pertenencia: "Ambigua"),
                new EventoBus.MarcacionDelDia(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA", Pertenencia: "DelTurno")
            ],
            new EventoBus.HorasDiscriminadas(new Dictionary<string, decimal>(), []));

        await WhenAsync(recibido);

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, "Turno Manana", [],
            [
                new MarcacionDelDia(new DateTime(2026, 3, 15, 4, 30, 0), "ENTRADA", Pertenencia: "Ambigua"),
                new MarcacionDelDia(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA", Pertenencia: "DelTurno")
            ],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }
}
