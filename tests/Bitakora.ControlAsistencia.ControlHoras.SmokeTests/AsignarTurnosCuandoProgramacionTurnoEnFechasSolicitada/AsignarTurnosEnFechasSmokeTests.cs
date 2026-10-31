using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using Microsoft.Extensions.Configuration;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.AsignarTurnosCuandoProgramacionTurnoEnFechasSolicitada;

public class AsignarTurnosEnFechasSmokeTests(ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private const string RutaSolicitud = "/api/programacion/solicitudes-en-fechas";
    private const string TopicDiasDepurados = "dias-depurados";
    private const string SuscripcionSmokeTests = "smoke-tests";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoTurnoDiarioAsignado = "turno_diario_asignado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly DateOnly[] Fechas = [new(2026, 4, 13), new(2026, 4, 14)];

    private static HttpClient CrearClienteProgramacion()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var baseUrl = configuration["Programacion:BaseUrl"]
            ?? throw new InvalidOperationException(
                "Programacion:BaseUrl no esta configurado. Usa appsettings.json, appsettings.local.json o la variable Programacion__BaseUrl.");

        var identidad = IdentidadDePrueba.Desde(configuration);
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Add("X-Tenant-Id", identidad.TenantId);
        client.DefaultRequestHeaders.Add("X-User-Id", identidad.UserId);
        client.DefaultRequestHeaders.Add("X-Organization-Membership-Id", identidad.OrganizationMembershipId);
        return client;
    }

    private static async Task<Guid> CrearTurnoAsync(HttpClient programacion, CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var response = await programacion.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno Smoke Lote {turnoId}",
            ordinarias = new[]
            {
                new
                {
                    inicio = "08:00:00",
                    fin = "16:00:00",
                    descansos = Array.Empty<object>(),
                    extras = Array.Empty<object>()
                }
            }
        }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return turnoId;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_PublicaDiasDepuradosConLasFotosDelLote_CuandoSeSolicitanDosFechas()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        using var programacion = CrearClienteProgramacion();
        await serviceBus.PurgeAsync(TopicDiasDepurados, SuscripcionSmokeTests);
        var turnoId = await CrearTurnoAsync(programacion, ct);
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();

        var solicitud = await programacion.PostAsJsonAsync(RutaSolicitud, new
        {
            id = solicitudId,
            turnoId,
            colaborador = new
            {
                identificacion = "CC-555666999",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke Lote En Fechas"
            },
            fechas = Fechas.Select(f => f.ToString("yyyy-MM-dd")).ToArray()
        }, ct);

        solicitud.StatusCode.Should().Be(HttpStatusCode.Created);

        var (diasDepurados, sessionId) = await serviceBus.WaitForMessageConSesionAsync<DiasDepurados>(
            TopicDiasDepurados, SuscripcionSmokeTests, e => e.CodigoColaborador == codigoColaborador, Timeout);

        sessionId.Should().Be(codigoColaborador);
        diasDepurados.Dias.Select(d => d.Fecha).Should().Equal(Fechas);
        diasDepurados.Dias.Should().OnlyContain(d => d.NombreTurno == $"[TEST] Turno Smoke Lote {turnoId}");

        foreach (var fecha in Fechas)
        {
            var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";
            var persistido = await postgres.ExisteEventoAsync(
                SchemaControlHoras, streamId, TipoTurnoDiarioAsignado, Timeout,
                campoJson: "SolicitudId", valorJson: solicitudId.ToString());
            persistido.Should().BeTrue(
                $"el lote deberia persistir {TipoTurnoDiarioAsignado} con SolicitudId {solicitudId} en {streamId}");
        }
    }

    private async Task<JsonElement> PublicarYLeerAsync(
        string codigoColaborador, Guid solicitudId, DateOnly fecha, object evento)
    {
        await serviceBus.PublishAsync(codigoColaborador, ProgramacionEnLote.Topic, evento, solicitudId.ToString());

        var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";
        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoTurnoDiarioAsignado, Timeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());
        existe.Should().BeTrue($"{TipoTurnoDiarioAsignado} deberia existir en {streamId}");

        return await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, TipoTurnoDiarioAsignado,
            "SolicitudId", solicitudId.ToString(), TimeSpan.FromSeconds(5));
    }

    private async Task AfirmarSinDeadLetterAsync(string codigoColaborador)
    {
        var existeDeadLetterEnQueue = await serviceBus.ExisteDeadLetterEnQueueDeColaboradorAsync(
            ServiceBusFixture.QueueFanIn, codigoColaborador);

        existeDeadLetterEnQueue.Should().BeFalse(
            "no deberia haber un dead letter del colaborador {0} en el queue '{1}'",
            codigoColaborador, ServiceBusFixture.QueueFanIn);
    }

    private static Dictionary<string, object?> EventoEnLote(
        Guid solicitudId, string codigoColaborador, string identificacion, DateOnly fecha, object detalleTurno,
        object? jornada = null) =>
        new()
        {
            ["SolicitudId"] = solicitudId,
            ["Colaborador"] = new
            {
                Identificacion = identificacion,
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Lote Asignacion"
            },
            ["Fechas"] = new[] { fecha.ToString("yyyy-MM-dd") },
            ["DetalleTurno"] = detalleTurno,
            ["Jornada"] = jornada ?? ProgramacionEnLote.Jornada
        };

    private static object FranjaOcho(object? sede = null) =>
        new
        {
            HoraInicio = "08:00:00",
            HoraFin = "16:00:00",
            DiaOffsetFin = 0,
            Descansos = Array.Empty<object>(),
            Extras = Array.Empty<object>(),
            Sede = sede
        };

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_PersisteElTurnoConSedePorFranja_CuandoElEventoTraeSedesEfectivas()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 11);
        var detalleTurno = new
        {
            Nombre = "[TEST] Turno Partido Sede Lote",
            FranjasOrdinarias = new object[]
            {
                new
                {
                    HoraInicio = "06:00:00", HoraFin = "10:00:00", DiaOffsetFin = 0,
                    Descansos = Array.Empty<object>(), Extras = Array.Empty<object>(),
                    Sede = new { Id = "SEDE-SUBA-TEST", Nombre = "[TEST] Suba" }
                },
                new
                {
                    HoraInicio = "14:00:00", HoraFin = "18:00:00", DiaOffsetFin = 0,
                    Descansos = Array.Empty<object>(), Extras = Array.Empty<object>()
                }
            }
        };

        var persistido = await PublicarYLeerAsync(codigoColaborador, solicitudId, fecha,
            EventoEnLote(solicitudId, codigoColaborador, "CC-222333444", fecha, detalleTurno));

        var esperado = new TurnoDiario("[TEST] Turno Partido Sede Lote", [
            new FranjaProgramada(
                new TimeOnly(6, 0), new TimeOnly(10, 0), 0,
                Array.Empty<SubFranjaProgramada>(), Array.Empty<SubFranjaProgramada>(), "",
                new SedeProgramada("SEDE-SUBA-TEST", "[TEST] Suba")),
            new FranjaProgramada(
                new TimeOnly(14, 0), new TimeOnly(18, 0), 0,
                Array.Empty<SubFranjaProgramada>(), Array.Empty<SubFranjaProgramada>(), "")
        ], "");
        persistido.GetProperty("DetalleTurno").Deserialize<TurnoDiario>()
            .Should().BeEquivalentTo(esperado, opciones => opciones.ExcludingMembersNamed("Descripcion"));

        await AfirmarSinDeadLetterAsync(codigoColaborador);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_PersisteElCentroDeCostosDeLaSedeDeLaFranja_CuandoElEventoLoTrae()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 13);
        const string centroDeCostos = "CC-300-LOGISTICA";
        var detalleTurno = new
        {
            Nombre = "[TEST] Turno Con CC Lote",
            FranjasOrdinarias = new[]
            {
                FranjaOcho(new { Id = "SEDE-CC-04", Nombre = "[TEST] Sede Con Costeo Lote", CentroDeCostos = centroDeCostos })
            }
        };

        var persistido = await PublicarYLeerAsync(codigoColaborador, solicitudId, fecha,
            EventoEnLote(solicitudId, codigoColaborador, "CC-333444555", fecha, detalleTurno));

        var turno = persistido.GetProperty("DetalleTurno").Deserialize<TurnoDiario>();
        turno!.FranjasOrdinarias.Should().ContainSingle();
        turno.FranjasOrdinarias[0].Sede.Should().Be(
            new SedeProgramada("SEDE-CC-04", "[TEST] Sede Con Costeo Lote", centroDeCostos));

        await AfirmarSinDeadLetterAsync(codigoColaborador);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_PersisteLaJornadaEstampada_CuandoElEventoLaTrae()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 12);
        var jornadaId = Guid.CreateVersion7();
        var detalleTurno = new { Nombre = "[TEST] Turno Jornada Lote", FranjasOrdinarias = new[] { FranjaOcho() } };
        var jornada = new
        {
            JornadaId = jornadaId,
            HorasSemanalesEnMinutos = 2520,
            TopeDiarioEnMinutos = 510,
            MinimoDiarioEnMinutos = 240,
            DiasDescansoPorSemana = 1
        };

        var persistido = await PublicarYLeerAsync(codigoColaborador, solicitudId, fecha,
            EventoEnLote(solicitudId, codigoColaborador, "CC-555666777", fecha, detalleTurno, jornada));

        persistido.GetProperty("Jornada").Deserialize<JornadaProgramada>()
            .Should().Be(new JornadaProgramada(jornadaId, 2520, 510, 240, 1));

        await AfirmarSinDeadLetterAsync(codigoColaborador);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_PersisteLaAmplitudDeLaVentanaDeEntrada_CuandoElEventoLaTrae()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 13);
        var detalleTurno = new
        {
            Nombre = "[TEST] Turno Ventana Lote",
            FranjasOrdinarias = new[] { FranjaOcho() },
            AmplitudVentanaEntradaEnMinutos = 180
        };

        var persistido = await PublicarYLeerAsync(codigoColaborador, solicitudId, fecha,
            EventoEnLote(solicitudId, codigoColaborador, "CC-555666778", fecha, detalleTurno));

        persistido.GetProperty("DetalleTurno").GetProperty("AmplitudVentanaEntradaEnMinutos")
            .GetInt32().Should().Be(180);

        await AfirmarSinDeadLetterAsync(codigoColaborador);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_PersisteLaMarcaDeHorarioAbierto_CuandoElEventoLaTrae()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 14);
        var detalleTurno = new
        {
            Nombre = "[TEST] Turno Horario Abierto Lote",
            FranjasOrdinarias = new[] { FranjaOcho() },
            EsHorarioAbierto = true
        };

        var persistido = await PublicarYLeerAsync(codigoColaborador, solicitudId, fecha,
            EventoEnLote(solicitudId, codigoColaborador, "CC-555666779", fecha, detalleTurno));

        persistido.GetProperty("DetalleTurno").GetProperty("EsHorarioAbierto")
            .GetBoolean().Should().BeTrue();

        await AfirmarSinDeadLetterAsync(codigoColaborador);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ProgramacionTurnoEnFechasSolicitada_AsignaElTurno_CuandoElMensajeTieneFormatoCamelCaseDeWolverine()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 4, 10);
        var eventoEnFormatoWolverine = new
        {
            solicitudId,
            colaborador = new
            {
                identificacion = "CC-111222333",
                codigoColaborador,
                nombreCompleto = "[TEST] Smoke Lote CamelCase"
            },
            fechas = new[] { fecha.ToString("yyyy-MM-dd") },
            detalleTurno = new
            {
                nombre = "[TEST] Turno Lote CamelCase",
                franjasOrdinarias = new[]
                {
                    new
                    {
                        horaInicio = "07:00:00", horaFin = "15:00:00", diaOffsetFin = 0,
                        descansos = Array.Empty<object>(), extras = Array.Empty<object>()
                    }
                }
            },
            jornada = new
            {
                jornadaId = ProgramacionEnLote.JornadaId,
                horasSemanalesEnMinutos = 2880,
                topeDiarioEnMinutos = 1440,
                minimoDiarioEnMinutos = 0,
                diasDescansoPorSemana = 1
            }
        };

        var persistido = await PublicarYLeerAsync(codigoColaborador, solicitudId, fecha, eventoEnFormatoWolverine);

        persistido.GetProperty("InformacionColaborador").Deserialize<ColaboradorProgramado>()
            .Should().Be(new ColaboradorProgramado(
                "CC-111222333", codigoColaborador, "[TEST] Smoke Lote CamelCase"));
        var esperado = new TurnoDiario("[TEST] Turno Lote CamelCase", [
            new FranjaProgramada(
                new TimeOnly(7, 0), new TimeOnly(15, 0), 0,
                Array.Empty<SubFranjaProgramada>(), Array.Empty<SubFranjaProgramada>(), "")
        ], "");
        persistido.GetProperty("DetalleTurno").Deserialize<TurnoDiario>()
            .Should().BeEquivalentTo(esperado, opciones => opciones.ExcludingMembersNamed("Descripcion"));

        await AfirmarSinDeadLetterAsync(codigoColaborador);
    }
}
