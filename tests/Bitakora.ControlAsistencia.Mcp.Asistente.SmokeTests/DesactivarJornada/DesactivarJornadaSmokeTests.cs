using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.DesactivarJornada;

public class DesactivarJornadaSmokeTests(McpFixture mcp)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    private async Task<string> DesactivarAsync(string jornadaId, CancellationToken ct)
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "desactivar_jornada", new Dictionary<string, object?> { ["jornada_id"] = jornadaId }, cancellationToken: ct);
        return SembradoDeJornadas.Texto(resultado);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_DesactivaYLuegoRespondeExitoDeNuevo_CuandoLaJornadaNoLaUsaNadie()
    {
        var ct = TestContext.Current.CancellationToken;
        var (jornada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;

        var primera = await DesactivarAsync(jornadaId, ct);
        var repetida = await DesactivarAsync(jornadaId, ct);

        primera.Should().Be("Jornada desactivada.");
        repetida.Should().Be("Jornada desactivada.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_RespondeQueLaUsaUnColaborador_CuandoSeAsignoAUnColaboradorRegistrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var (jornada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;
        var (identificacion, _) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, null, ct);
        await mcp.Cliente.CallToolAsync(
            "asignar_jornada",
            new Dictionary<string, object?> { ["jornada_id"] = jornadaId, ["identificaciones"] = identificacion },
            cancellationToken: ct);

        var frase = await Polling.WaitUntilAsync(
            async () =>
            {
                var texto = await DesactivarAsync(jornadaId, ct);
                return texto.Contains("1 colaborador vigente", StringComparison.Ordinal) ? texto : null;
            },
            TimeoutPolling);

        frase.Should().Contain("1 colaborador vigente");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DesactivarJornada_RespondeElRechazoDelServidor_CuandoLaJornadaEsLaPredeterminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var predeterminada = await SembradoDeJornadas.PredeterminadaActualAsync(mcp, ct);

        var frase = await DesactivarAsync(predeterminada.GetProperty("jornada_id").GetString()!, ct);

        frase.Should().StartWith("El dominio rechazo la solicitud:");
    }
}
