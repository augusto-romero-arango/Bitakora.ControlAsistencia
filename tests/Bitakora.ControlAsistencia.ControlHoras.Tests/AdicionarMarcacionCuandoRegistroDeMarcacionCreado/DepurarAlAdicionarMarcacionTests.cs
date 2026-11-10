using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
// Homonimos en ControlHoras.DomainEvents; este archivo usa los del bus.
using DiaDepurado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.DiaDepurado;
using FranjaDepurada = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.FranjaDepurada;
using MarcacionDelDia = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.MarcacionDelDia;
using HorasDiscriminadas = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.HorasDiscriminadas;
using RegistroDeMarcacionCreado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.RegistroDeMarcacionCreado;
using ResumenColaborador = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.AdicionarMarcacionCuandoRegistroDeMarcacionCreado;

public class DepurarAlAdicionarMarcacionTests : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"cd:{CodigoColaborador}:{Fecha:yyyyMMdd}";
    private static readonly string StreamIdDiaAnterior = $"cd:{CodigoColaborador}:20260314";

    private static readonly ColaboradorProgramado ColaboradorPersistido = new(
        "CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    // Literal separado de ColaboradorPersistido a proposito: una permutacion de campos en
    // CrearResumenColaborador() se delata aqui.
    private static readonly ResumenColaborador ColaboradorResumen = new(
        "CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");
    private static readonly Guid SolicitudId = Guid.Parse("019600c0-0000-7000-8000-000000000001");

    // Descripcion (dato derivado) no interviene en estos tests: placeholder "".
    private static readonly FranjaProgramada Franja06_14 =
        new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "");

    private static readonly FranjaProgramada Franja06_12 =
        new(new TimeOnly(6, 0), new TimeOnly(12, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja14_18 =
        new(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, [], [], "");
    private static readonly FranjaProgramada Franja07_16 =
        new(new TimeOnly(7, 0), new TimeOnly(16, 0), 0, [], [], "");

    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp05_50 = new(2026, 3, 15, 5, 50, 0);
    private static readonly DateTime Timestamp12_05 = new(2026, 3, 15, 12, 5, 0);
    private static readonly DateTime Timestamp14_10 = new(2026, 3, 15, 14, 10, 0);

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PrivateEventSender);

    private static RegistroDeMarcacionCreado CrearRegistroDeMarcacionCreado(DateTime timestamp) =>
        new(CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static MarcacionAdicionada CrearMarcacionAdicionada(DateTime timestamp) =>
        new(StreamId, CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado CrearTurnoDiarioAsignado(TurnoDiario detalleTurno) =>
        new(StreamId, ColaboradorPersistido, Fecha, detalleTurno, SolicitudId);

    private static DateTime En(int hora, int minuto) => Fecha.ToDateTime(new TimeOnly(hora, minuto));

    // And<>() compara con BeEquivalentTo, que ignora el orden: el string fija ademas que cada
    // pertenencia queda junto a su marcacion.
    private static string PertenenciasPorHora(DiaDepurado dia) =>
        string.Join(" ", dia.Marcaciones.Select(m => $"{m.Timestamp:HH:mm}={m.Pertenencia}"));

    // Oraculo independiente: se arma desde IntervaloTemporal.ToString() y la etiqueta del recurso, sin
    // ejecutar Discriminar.
    private static string LineaConcepto(TimeOnly inicio, TimeOnly fin, Concepto concepto) =>
        $"{IntervaloTemporal.Crear(new MomentoDelDia(inicio), new MomentoDelDia(fin))}: " +
        $"{IntervaloClasificado.Mensajes.Etiqueta(concepto)}";

    [Fact]
    public async Task RegistroDeMarcacionCreado_CalculaControlFranja_CuandoHayTurnoYLlegaMarcacion()
    {
        var turnoUnico = new TurnoDiario("Turno Manana", [Franja06_14], "");
        Given(StreamId, CrearTurnoDiarioAsignado(turnoUnico));

        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[] { new(Franja06_14, Timestamp07_00, null) });

        // El contrato plano no distingue franja anomala de dia sin horas: ambos publican el diccionario
        // vacio (riesgo aceptado).
        ThenIsPublishedPrivately(new PublishOptions { GroupId = CodigoColaborador }, new DiaDepurado(
            CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Manana",
            [new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, Timestamp07_00, null, true)],
            [new MarcacionDelDia(Timestamp07_00, "ENTRADA", Pertenencia: "DelTurno")],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), VersionControlDiario: 2));
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_DejaControlesDeFranjaVacios_CuandoNoHayTurnoPrevio()
    {
        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp07_00));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp07_00));
        And<ControlDiarioAggregateRoot, int>(
            StreamId, c => c.ControlesDeFranja.Count, 0);

        ThenIsPublishedPrivately(new PublishOptions { GroupId = CodigoColaborador }, new DiaDepurado(
            CodigoColaborador,
            Fecha,
            null,
            null,
            [],
            [new MarcacionDelDia(Timestamp07_00, "ENTRADA")],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), []), VersionControlDiario: 1));
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_RecalculaControlesDeFranjaCompletos_CuandoHayTurnoPartidoYMarcacionesAcumuladas()
    {
        var turnoPartido = new TurnoDiario("Turno Partido", [Franja06_12, Franja14_18], "");
        Given(StreamId,
            CrearTurnoDiarioAsignado(turnoPartido),
            CrearMarcacionAdicionada(Timestamp05_50),
            CrearMarcacionAdicionada(Timestamp12_05));

        await WhenAsync(CrearRegistroDeMarcacionCreado(Timestamp14_10));

        Then(StreamId, CrearMarcacionAdicionada(Timestamp14_10));
        And<ControlDiarioAggregateRoot, IReadOnlyList<ControlFranja>>(
            StreamId,
            c => c.ControlesDeFranja,
            new ControlFranja[]
            {
                new(Franja06_12, Timestamp05_50, Timestamp12_05),
                new(Franja14_18, Timestamp14_10, null)
            });

        // Esperado a mano, sin ejecutar Discriminar ni Consolidar: F1 trabajada 05:50-12:05 en domingo se
        // recorta a 06:00 (sin retardo); 06:00-12:00 = 6.00h DominicalFestivaDiurna y el excedente
        // 12:00-12:05 = 5 min = 0.08h ExtraDiurnaDominicalFestiva. F2 es anomala (Salida null): no aporta
        // horas ni lineas de trazabilidad.
        ThenIsPublishedPrivately(new PublishOptions { GroupId = CodigoColaborador }, new DiaDepurado(
            CodigoColaborador,
            Fecha,
            ColaboradorResumen,
            "Turno Partido",
            [
                new FranjaDepurada(new TimeOnly(6, 0), new TimeOnly(12, 0), 0, Timestamp05_50, Timestamp12_05, false),
                new FranjaDepurada(new TimeOnly(14, 0), new TimeOnly(18, 0), 0, Timestamp14_10, null, true)
            ],
            [
                new MarcacionDelDia(Timestamp05_50, "ENTRADA", Pertenencia: "DelTurno"),
                new MarcacionDelDia(Timestamp12_05, "ENTRADA", Pertenencia: "DelTurno"),
                new MarcacionDelDia(Timestamp14_10, "ENTRADA", Pertenencia: "DelTurno")
            ],
            new HorasDiscriminadas(
                new Dictionary<string, decimal>
                {
                    ["DominicalFestivaDiurna"] = 6.00m,
                    ["ExtraDiurnaDominicalFestiva"] = 0.08m
                },
                [
                    LineaConcepto(new TimeOnly(6, 0), new TimeOnly(12, 0), Concepto.DominicalFestivaDiurna),
                    LineaConcepto(new TimeOnly(12, 0), new TimeOnly(12, 5), Concepto.ExtraDiurnaDominicalFestiva)
                ]), VersionControlDiario: 4));
    }

    // La marcacion de madrugada se guarda solo en su dia calendario; el reparto la lleva a la vispera.
    [Fact]
    public async Task RegistroDeMarcacionCreado_PublicaUnSoloDiaDepurado_CuandoMarcacionEstaEnMadrugada()
    {
        var timestampNocturno = new DateTime(2026, 3, 15, 2, 0, 0);

        await WhenAsync(CrearRegistroDeMarcacionCreado(timestampNocturno));

        Then(StreamId,
            new MarcacionAdicionada(StreamId, CodigoColaborador, timestampNocturno, "ENTRADA", "DEV-001"));
        Then(StreamIdDiaAnterior);

        var marcacionNocturna = new MarcacionDelDia(timestampNocturno, "ENTRADA");
        ThenIsPublishedPrivately(
            new PublishOptions { GroupId = CodigoColaborador },
            new DiaDepurado(
                CodigoColaborador, Fecha, null, null, [], [marcacionNocturna],
                new HorasDiscriminadas(new Dictionary<string, decimal>(), []), VersionControlDiario: 1));

        And<ControlDiarioAggregateRoot, int>(
            StreamId, c => c.ControlesDeFranja.Count, 0);
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_ClasificaFueraDeCobertura_CuandoLaMarcacionPasaDeFinMasCuatroHoras()
    {
        Given(StreamId,
            CrearTurnoDiarioAsignado(new TurnoDiario("Turno Dia", [Franja07_16], "")),
            CrearMarcacionAdicionada(En(7, 10)),
            CrearMarcacionAdicionada(En(12, 0)),
            CrearMarcacionAdicionada(En(16, 5)));

        await WhenAsync(CrearRegistroDeMarcacionCreado(En(20, 15)));

        Then(StreamId, CrearMarcacionAdicionada(En(20, 15)));
        And<ControlDiarioAggregateRoot, string>(
            StreamId,
            c => PertenenciasPorHora(c.CrearDiaDepurado()),
            "07:10=DelTurno 12:00=DelTurno 16:05=DelTurno 20:15=FueraDeCobertura");
    }

    [Fact]
    public async Task RegistroDeMarcacionCreado_ClasificaAmbigua_CuandoLaMarcacionCaeTambienEnLaCoberturaDeLaVispera()
    {
        Given(StreamId,
            CrearTurnoDiarioAsignado(new TurnoDiario("Turno Manana", [Franja06_14], "")),
            CoberturaVecinaActualizada.Crear(StreamId, DiaVecino.Vispera, En(5, 0)),
            CrearMarcacionAdicionada(En(6, 20)));

        await WhenAsync(CrearRegistroDeMarcacionCreado(En(4, 30)));

        Then(StreamId, CrearMarcacionAdicionada(En(4, 30)));
        And<ControlDiarioAggregateRoot, string>(
            StreamId,
            c => PertenenciasPorHora(c.CrearDiaDepurado()),
            "04:30=Ambigua 06:20=DelTurno");
    }
}
