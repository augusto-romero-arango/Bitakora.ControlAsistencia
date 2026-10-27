using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.SolicitarProgramacionTurno;

public partial class SolicitarProgramacionTurnoSmokeTests(
    McpFixture mcp, ProgramacionApiFixture programacion, ColaboradoresApiFixture colaboradores)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TimeoutAdvertencias = TimeSpan.FromSeconds(60);

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex PatronGuid();

    // Tope diario de 1 h: un turno de 8 h lo supera, cosa que con la predeterminada (8 h) no pasa.
    // Las horas semanales al azar reducen el 409 por limites repetidos; si llega, se reutiliza la
    // Jornada que el 409 nombra.
    private async Task<Guid> CrearJornadaDeTopeDiarioUnaHoraAsync(CancellationToken ct)
    {
        var jornadaId = Guid.CreateVersion7();
        var respuesta = await programacion.Client.PostAsJsonAsync("/api/programacion/jornadas", new
        {
            jornadaId,
            horasSemanales = new { horas = Random.Shared.Next(1, 8), minutos = 0 },
            topeDiario = new { horas = 1, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 0
        }, ct);

        if (respuesta.StatusCode == HttpStatusCode.Created)
            return jornadaId;

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var coincidencia = PatronGuid().Match(await respuesta.Content.ReadAsStringAsync(ct));
        coincidencia.Success.Should().BeTrue("el 409 deberia nombrar la Jornada existente");
        return Guid.Parse(coincidencia.Value);
    }

    private async Task SembrarTurnoAsync(Guid turnoId, string nombre, CancellationToken ct)
    {
        var respuesta = await programacion.Client.PostAsJsonAsync(
            "/api/programacion/turnos", new { turnoId, nombre }, ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearTurno funcione en Programacion");

        var franja = await programacion.Client.PostAsJsonAsync(
            $"/api/programacion/turnos/{turnoId}:agregar-franja",
            new { inicio = "08:00:00", fin = "16:00:00" }, ct);
        franja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Polling.WaitUntilAsync(
            async () =>
            {
                using var candidata = await programacion.Client.BuscarFichaAsync(nombre, ct);
                return candidata?.RootElement.GetProperty("completo").GetBoolean() == true ? new object() : null;
            }, TimeoutPolling);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_InformaLosDiasRespetados_CuandoElColaboradorTieneUnaAusenciaEnLaVentana()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var codigoSede = $"TEST-{sufijo}";
        var codigoColaborador = $"TEST-{sufijo}";
        var numeroIdentificacion = sufijo.ToString("N").ToUpperInvariant();
        var identificacion = $"CC-{numeroIdentificacion}";
        var nombreTurno = $"[TEST] Turno MCP {sufijo}";

        await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?>
            {
                ["codigo"] = codigoSede,
                ["nombre"] = "[TEST] Sede MCP Programacion"
            },
            cancellationToken: ct);
        await mcp.Cliente.CallToolAsync(
            "registrar_colaborador",
            new Dictionary<string, object?>
            {
                ["tipo_identificacion"] = "CC",
                ["numero_identificacion"] = numeroIdentificacion,
                ["primer_nombre"] = "[TEST]",
                ["primer_apellido"] = "MCP",
                ["codigo_colaborador"] = codigoColaborador,
                ["fecha_inicio"] = "2026-09-01"
            },
            cancellationToken: ct);
        var turnoId = Guid.CreateVersion7();
        await SembrarTurnoAsync(turnoId, nombreTurno, ct);

        using var ausencia = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "programar_ausencia",
                    new Dictionary<string, object?>
                    {
                        ["identificacion"] = identificacion,
                        ["desde"] = "2026-09-02",
                        ["hasta"] = "2026-09-02",
                        ["motivo"] = "Vacaciones"
                    },
                    cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);
                if (candidato.RootElement.TryGetProperty("motivo", out _))
                    return candidato;
                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-09-01",
            ["hasta"] = "2026-09-03",
            ["turno_id"] = turnoId.ToString(),
            ["sede_de_programacion"] = codigoSede,
            ["identificaciones"] = identificacion
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "solicitar_programacion_turno", argumentos, cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);

                var contieneAlColaborador = candidato.RootElement.GetProperty("programados").EnumerateArray()
                    .Any(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
                if (contieneAlColaborador)
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var programado = documento.RootElement.GetProperty("programados").EnumerateArray()
            .Single(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
        programado.GetProperty("dias").GetInt32().Should().Be(2, "el dia 2026-09-02 se respeta por ausencia");
        var respetado = programado.GetProperty("respetados").EnumerateArray().Single();
        respetado.GetProperty("motivo").GetString().Should().Be("Vacaciones");
        respetado.GetProperty("tramos").GetString().Should().Be("2");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_ProgramaConLaSedeDelColaborador_CuandoNoSeIndicaSedeDeProgramacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        var (identificacion, codigoColaborador) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        var nombreTurno = $"[TEST] Turno MCP {Guid.CreateVersion7()}";
        var turnoId = Guid.CreateVersion7();
        await SembrarTurnoAsync(turnoId, nombreTurno, ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-09-01",
            ["hasta"] = "2026-09-03",
            ["turno_id"] = turnoId.ToString(),
            ["identificaciones"] = identificacion
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "solicitar_programacion_turno", argumentos, cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);

                if (candidato.RootElement.TryGetProperty("programados", out var programados)
                    && programados.EnumerateArray().Any(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador))
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var resultado = documento.RootElement;
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse();
        (!resultado.TryGetProperty("sede", out var sedeNivelSuperior) || sedeNivelSuperior.ValueKind == JsonValueKind.Null)
            .Should().BeTrue("no hubo sede explicita");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_EstampaLaJornadaAsignada_CuandoElColaboradorTieneUnaAsignada()
    {
        var ct = TestContext.Current.CancellationToken;
        var jornadaId = await CrearJornadaDeTopeDiarioUnaHoraAsync(ct);
        var (identificacion, codigoColaborador) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, null, ct);
        await colaboradores.AsignarJornadaAsync(identificacion, jornadaId, ct);
        await Polling.WaitUntilAsync(
            async () => await colaboradores.JornadaEnDirectorioAsync(identificacion, ct) == jornadaId.ToString()
                ? new object()
                : null,
            TimeoutPolling);

        var nombreTurno = $"[TEST] Turno MCP {Guid.CreateVersion7()}";
        var turnoId = Guid.CreateVersion7();
        await SembrarTurnoAsync(turnoId, nombreTurno, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var dia = hoy.AddDays((8 - (int)hoy.DayOfWeek) % 7 + 14).ToString("yyyy-MM-dd");
        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = dia,
            ["hasta"] = dia,
            ["turno_id"] = turnoId.ToString(),
            ["identificaciones"] = identificacion
        };

        using var _ = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "solicitar_programacion_turno", argumentos, cancellationToken: ct);
                var candidato = Sembrado.LeerJson(respuesta);

                if (candidato.RootElement.TryGetProperty("programados", out var programados)
                    && programados.EnumerateArray().Any(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador))
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var advertenciaEsperada = $"{dia}: supera el tope diario";
        var advertencias = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "consultar_advertencias_programacion",
                    new Dictionary<string, object?>
                    {
                        ["fecha"] = dia,
                        ["codigos_colaborador"] = codigoColaborador
                    },
                    cancellationToken: ct);
                var texto = respuesta.Content.OfType<TextContentBlock>().Single().Text;
                if (!texto.TrimStart().StartsWith('{'))
                    return null;

                using var documento = JsonDocument.Parse(texto);
                var delColaborador = documento.RootElement.GetProperty("colaboradores").EnumerateArray()
                    .Where(c => c.GetProperty("codigo").GetString() == codigoColaborador)
                    .SelectMany(c => c.GetProperty("advertencias").EnumerateArray().Select(a => a.GetString()!))
                    .ToList();
                return delColaborador.Any(a => a.StartsWith(advertenciaEsperada, StringComparison.Ordinal))
                    ? delColaborador
                    : null;
            },
            TimeoutAdvertencias);

        advertencias.Should().Contain(a => a.StartsWith(advertenciaEsperada, StringComparison.Ordinal),
            "la Jornada estampada es la asignada (tope diario 1 h), no la predeterminada (8 h)");
    }
}
