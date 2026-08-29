// Issue #425: DiaCalculado recibe cada DiaDepurado, mantiene los valores provisionales del dia y
// nace Provisional. Sin comparacion contra el estado previo, sin deduplicacion de ningun tipo
// (decision de sesion 2026-08-24): toda entrega se persiste, incluida la del dia sin jornada valida.
// Issue #484: MapearFranja/MapearMarcacion dejan de descartar la sede programada/marcada al
// traducir bus -> isla persistida.

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

    // ---- Datos de entrada: DiaDepurado tal como lo publica el productor (PrivateEvents) ----

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

    // ---- Oraculo independiente (regla 20): el evento persistido esperado se arma a mano con los
    //      tipos ricos propios de ControlHoras.DomainEvents, sin reusar el mapeo que ejercita el
    //      handler bajo prueba. ----

    private static ResumenColaborador ColaboradorEsperado() =>
        new("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    private static FranjaDepurada FranjaEsperada() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false);

    private static MarcacionDelDia MarcacionEsperada() =>
        new(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA");

    private static HorasDiscriminadas HorasEsperadas() => new(
        new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }, []);

    // ---- Issue #484: datos de entrada y oraculo para la sede programada/marcada ----

    private static readonly EventoBus.FranjaDepurada FranjaConSedeProgramadaRecibida = new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false,
        "SEDE-01", "Sede Principal", "CC-100");

    private static readonly EventoBus.MarcacionDelDia MarcacionConSedeMarcadaRecibida = new(
        new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA",
        "SEDE-02", "Sede Norte", "CC-200");

    private static FranjaDepurada FranjaEsperadaConSedeProgramada() => new(
        new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
        new DateTime(2026, 3, 15, 6, 0, 0), new DateTime(2026, 3, 15, 14, 0, 0), false,
        "SEDE-01", "Sede Principal", "CC-100");

    private static MarcacionDelDia MarcacionEsperadaConSedeMarcada() => new(
        new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA",
        "SEDE-02", "Sede Norte", "CC-200");

    // CA-1: la franja trae sede programada -> el persistido lleva codigo/nombre/CC programados.
    // La marcacion sigue sin sede (null), demostrando que el campo opuesto no se contamina (CA-3).
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

    // CA-2: la marcacion trae sede estampada -> el persistido lleva codigo/nombre/CC de la marcacion.
    // La franja sigue sin sede programada (null), demostrando que el campo opuesto no se contamina
    // (CA-3).
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

    // CA-1: dia nuevo -> crea el stream dc:{codigo}:{yyyyMMdd} con DepuracionDiaRecibida (foto
    // completa) y el dia queda Provisional.
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

    // CA-2: dia existente -> se agrega otra DepuracionDiaRecibida al mismo stream y los valores
    // provisionales son los de la ultima foto (la foto previa no tenia turno ni franjas).
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

    // CA-3: dia sin jornada valida (Colaborador/NombreTurno null, franjas y horas vacias,
    // marcaciones crudas) nace igual, con su foto persistida.
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

    // CA-4: dos entregas identicas producen dos eventos en el stream, sin dedup. Then verifica
    // solo el evento NUEVO emitido en este WhenAsync (Given no cuenta como "nuevo").
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
}
