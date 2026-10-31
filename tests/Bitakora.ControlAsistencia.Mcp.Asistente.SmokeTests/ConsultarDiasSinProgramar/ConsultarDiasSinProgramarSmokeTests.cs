using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ConsultarDiasSinProgramar;

public partial class ConsultarDiasSinProgramarSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(60);

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex PatronGuid();

    private async Task<Guid> CrearJornadaAsync(CancellationToken ct)
    {
        var jornadaId = Guid.CreateVersion7();
        var respuesta = await programacion.Client.PostAsJsonAsync("/api/programacion/jornadas", new
        {
            jornadaId,
            horasSemanales = new { horas = 42, minutos = 0 },
            topeDiario = new { horas = 8, minutos = 0 },
            minimoDiario = new { horas = 0, minutos = 0 },
            diasDescansoPorSemana = 1
        }, ct);

        if (respuesta.StatusCode == HttpStatusCode.Created)
            return jornadaId;

        respuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var coincidencia = PatronGuid().Match(await respuesta.Content.ReadAsStringAsync(ct));
        coincidencia.Success.Should().BeTrue("el 409 deberia nombrar la Jornada existente");
        return Guid.Parse(coincidencia.Value);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarDiasSinProgramar_DevuelveLosDiasQueFaltan_CuandoSoloSeProgramaronLunesYMartes()
    {
        var ct = TestContext.Current.CancellationToken;
        var (identificacion, codigo) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, null, ct);

        var jornadaId = await CrearJornadaAsync(ct);
        var turnoId = Guid.CreateVersion7();
        var crearTurno = await programacion.Client.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno Dias Sin Programar MCP {turnoId}",
            ordinarias = new[]
            {
                new { inicio = "06:00:00", fin = "14:00:00", descansos = Array.Empty<object>(), extras = Array.Empty<object>() }
            }
        }, ct);
        crearTurno.StatusCode.Should().Be(HttpStatusCode.Created);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var lunes = hoy.AddDays((8 - (int)hoy.DayOfWeek) % 7 + 14);
        var fechas = new[] { lunes.ToString("yyyy-MM-dd"), lunes.AddDays(1).ToString("yyyy-MM-dd") };

        var solicitud = await programacion.Client.PostAsJsonAsync("/api/programacion/solicitudes-en-fechas", new
        {
            id = Guid.CreateVersion7(),
            turnoId,
            colaborador = new { identificacion, codigoColaborador = codigo, nombreCompleto = "[TEST] MCP" },
            fechas,
            jornadaId
        }, ct);
        solicitud.StatusCode.Should().Be(HttpStatusCode.Created);

        var esperado = $"{lunes.AddDays(2):yyyy-MM-dd} a {lunes.AddDays(6):yyyy-MM-dd}";

        var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "consultar_dias_sin_programar",
                    new Dictionary<string, object?>
                    {
                        ["fecha"] = lunes.ToString("yyyy-MM-dd"),
                        ["semanas"] = 1,
                        ["codigos_colaborador"] = codigo
                    },
                    cancellationToken: ct);
                respuesta.IsError.Should().NotBeTrue();
                var texto = respuesta.Content.OfType<TextContentBlock>().Single().Text;
                if (!texto.TrimStart().StartsWith('{'))
                    return null;

                var doc = JsonDocument.Parse(texto);
                var dias = doc.RootElement.GetProperty("colaboradores").EnumerateArray()
                    .Where(c => c.GetProperty("codigo").GetString() == codigo)
                    .SelectMany(c => c.GetProperty("diasSinProgramar").EnumerateArray())
                    .Select(d => d.GetString())
                    .ToList();
                return dias.SequenceEqual([esperado]) ? doc : null;
            },
            TimeoutPolling);
        using var documentoDisponible = documento;

        documento.RootElement.TryGetProperty("total", out _).Should().BeFalse();
    }
}
