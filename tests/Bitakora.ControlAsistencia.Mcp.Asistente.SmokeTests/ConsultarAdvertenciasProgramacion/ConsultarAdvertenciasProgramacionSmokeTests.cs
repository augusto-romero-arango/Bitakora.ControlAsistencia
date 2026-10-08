using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ConsultarAdvertenciasProgramacion;

public partial class ConsultarAdvertenciasProgramacionSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
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
    public async Task ConsultarAdvertenciasProgramacion_DevuelveAlColaboradorConAdvertencias_CuandoLaSemanaSuperaElTopeDiario()
    {
        var ct = TestContext.Current.CancellationToken;
        var (identificacion, codigo) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, null, ct);

        var jornadaId = await CrearJornadaAsync(ct);
        var turnoId = Guid.CreateVersion7();
        var crearTurno = await programacion.Client.PostAsJsonAsync("/api/programacion/turnos", new
        {
            turnoId,
            nombre = $"[TEST] Turno 10h Advertencias MCP {turnoId}",
            ordinarias = new[]
            {
                new { inicio = "06:00:00", fin = "16:00:00", descansos = Array.Empty<object>(), extras = Array.Empty<object>() }
            }
        }, ct);
        crearTurno.StatusCode.Should().Be(HttpStatusCode.Created);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var lunes = hoy.AddDays(7 + (8 - (int)hoy.DayOfWeek) % 7 + 7);
        var fechas = Enumerable.Range(0, 7).Select(i => lunes.AddDays(i).ToString("yyyy-MM-dd")).ToArray();

        var solicitud = await programacion.Client.PostAsJsonAsync("/api/programacion/solicitudes", new
        {
            id = Guid.CreateVersion7(),
            turnoId,
            colaborador = new { identificacion, codigoColaborador = codigo, nombreCompleto = "[TEST] MCP" },
            fechas,
            jornadaId
        }, ct);
        solicitud.StatusCode.Should().Be(HttpStatusCode.Created);

        var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "consultar_advertencias_programacion",
                    new Dictionary<string, object?>
                    {
                        ["fecha"] = lunes.ToString("yyyy-MM-dd"),
                        ["codigos_colaborador"] = codigo
                    },
                    cancellationToken: ct);
                respuesta.IsError.Should().NotBeTrue();
                var texto = respuesta.Content.OfType<TextContentBlock>().Single().Text;
                return texto.TrimStart().StartsWith('{') ? JsonDocument.Parse(texto) : null;
            },
            TimeoutPolling);
        using var documentoDisponible = documento;

        var raiz = documento.RootElement;
        raiz.GetProperty("desde").GetString().Should().Be(lunes.ToString("yyyy-MM-dd"));
        raiz.GetProperty("hasta").GetString().Should().Be(lunes.AddDays(6).ToString("yyyy-MM-dd"));
        raiz.TryGetProperty("total", out var total).Should().BeFalse();
        var colaborador = raiz.GetProperty("colaboradores").EnumerateArray()
            .Single(c => c.GetProperty("codigo").GetString() == codigo);
        colaborador.GetProperty("advertencias").GetArrayLength().Should().BeGreaterThan(0);
    }
}
