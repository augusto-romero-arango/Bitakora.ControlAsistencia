using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.RecibirDepuracionCuandoDiaDepurado;

// Verifica contra dev el efecto del consumidor nuevo: cada DiaDepurado que cruza el ASB interno
// persiste depuracion_dia_recibida en el stream de DiaCalculado. Se publica el evento directo al
// topic (no via POST de marcacion) para aislar este consumidor de la cadena que lo alimenta.
// Queda rojo hasta que el deploy publique la suscripcion control-horas-escucha-dia-depurado; el CI
// de PR no lo ejecuta (solo corre *.Tests).
public class RecibirDepuracionViaSbSmokeTests(ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string TopicDiaDepurado = "dia-depurado";
    private const string SuscripcionConsumidor = "control-horas-escucha-dia-depurado";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEvento = "depuracion_dia_recibida";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // En sync con DiaCalculadoAggregateRoot.ComputarStreamId (CA-ADR-0031: prefijo "dc" para no
    // colisionar con el "cd" de ControlDiario, que comparte colaborador+fecha en el mismo store).
    private static string ComputarStreamId(string codigoColaborador, DateOnly fecha) =>
        $"dc:{codigoColaborador}:{fecha:yyyyMMdd}";

    private static object CrearDiaDepurado(
        string codigoColaborador, DateOnly fecha, string? nombreTurno, bool conJornada) => new
        {
            CodigoColaborador = codigoColaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Colaborador = conJornada
                ? new
                {
                    Identificacion = "CC-555444333",
                    CodigoColaborador = codigoColaborador,
                    NombreCompleto = "[TEST] Smoke DiaCalculado"
                }
                : null,
            NombreTurno = nombreTurno,
            Franjas = conJornada
                ? new object[]
                {
                    new
                    {
                        HoraInicioProgramada = "06:00:00",
                        HoraFinProgramada = "14:00:00",
                        DiaOffsetFin = 0,
                        Entrada = $"{fecha:yyyy-MM-dd}T06:00:00",
                        Salida = $"{fecha:yyyy-MM-dd}T14:00:00",
                        EsAnomala = false
                    }
                }
                : [],
            Marcaciones = new object[]
            {
                new { Timestamp = $"{fecha:yyyy-MM-dd}T06:00:00", Tipo = "ENTRADA" }
            },
            HorasDiscriminadas = new
            {
                HorasPorConcepto = conJornada
                    ? new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }
                    : new Dictionary<string, decimal>(),
                Trazabilidad = Array.Empty<string>()
            }
        };

    // CA-1: un dia nuevo crea el stream dc:{codigo}:{yyyyMMdd} con la foto completa.
    // CA-2/CA-4: una segunda entrega sobre el mismo dia agrega otra depuracion_dia_recibida al mismo
    // stream -- sin deduplicacion. Las dos fotos se distinguen por NombreTurno para poder afirmar que
    // ambas quedaron persistidas.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DiaDepurado_PersisteCadaFotoEnElStreamDeDiaCalculado_CuandoLleganDosEntregasDelMismoDia()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 4);
        var streamId = ComputarStreamId(codigoColaborador, fecha);

        await serviceBus.PublishAsync(
            TopicDiaDepurado,
            CrearDiaDepurado(codigoColaborador, fecha, "[TEST] Turno Primera Foto", conJornada: true),
            Guid.CreateVersion7().ToString());

        var primeraFoto = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout,
            campoJson: "NombreTurno", valorJson: "[TEST] Turno Primera Foto");

        primeraFoto.Should().BeTrue(
            $"el evento {TipoEvento} de la primera foto deberia existir en el stream {streamId}");

        await serviceBus.PublishAsync(
            TopicDiaDepurado,
            CrearDiaDepurado(codigoColaborador, fecha, "[TEST] Turno Segunda Foto", conJornada: true),
            Guid.CreateVersion7().ToString());

        var segundaFoto = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout,
            campoJson: "NombreTurno", valorJson: "[TEST] Turno Segunda Foto");

        segundaFoto.Should().BeTrue(
            $"la segunda entrega deberia agregar otra {TipoEvento} al mismo stream {streamId}, sin dedup");

        var existeDeadLetter = await serviceBus.ExisteDeadLetterDeEstaCorridaAsync<DiaDepuradoMinimo>(
            TopicDiaDepurado, SuscripcionConsumidor, e => e.CodigoColaborador == codigoColaborador);

        existeDeadLetter.Should().BeFalse(
            "no deberia haber dead letter de esta corrida (CodigoColaborador {0}) en '{1}' del topic '{2}'",
            codigoColaborador, SuscripcionConsumidor, TopicDiaDepurado);
    }

    // CA-3: el dia sin jornada valida (Colaborador/NombreTurno null, franjas y horas vacias,
    // marcaciones crudas como unica evidencia) tambien crea su stream.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DiaDepurado_PersisteLaFoto_CuandoElDiaNoTieneJornadaValida()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 5, 5);
        var streamId = ComputarStreamId(codigoColaborador, fecha);

        await serviceBus.PublishAsync(
            TopicDiaDepurado,
            CrearDiaDepurado(codigoColaborador, fecha, nombreTurno: null, conJornada: false),
            Guid.CreateVersion7().ToString());

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEvento, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        existe.Should().BeTrue(
            $"el dia anomalo tambien deberia persistir {TipoEvento} en el stream {streamId}");
    }
}
