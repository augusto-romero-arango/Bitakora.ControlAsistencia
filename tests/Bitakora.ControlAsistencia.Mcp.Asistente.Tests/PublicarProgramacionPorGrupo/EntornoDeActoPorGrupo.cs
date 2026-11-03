using System.Net;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.PublicarProgramacionPorGrupo;

internal sealed class EntornoDeActoPorGrupo
{
    public const string RutaVista = "/api/programacion/borradores";
    public static readonly HttpMethod Query = new("QUERY");

    public static readonly TimeProvider RelojDeNoche =
        new RelojFalso(new DateTimeOffset(2026, 11, 4, 3, 0, 0, TimeSpan.Zero));

    public required HandlerPorRuta Programacion { get; init; }
    public required ColaboradoresFalso Colaboradores { get; init; }
    public required ProgramacionApi Api { get; init; }
    public required ResolutorSedePorCodigo ResolutorSedes { get; init; }
    public required ResolutorCandidatosPorGrupo ResolutorCandidatos { get; init; }

    public static string RutaActo(string codigo, string verbo) =>
        $"/api/programacion/colaboradores/{codigo}/borrador:{verbo}";

    public IReadOnlyList<(string Ruta, string? Cuerpo)> Actos(string verbo) =>
        [.. Programacion.Requests
            .Where(r => r.Metodo == HttpMethod.Post && r.Ruta.EndsWith($"/borrador:{verbo}", StringComparison.Ordinal))
            .Select(r => (r.Ruta, r.Cuerpo))];

    public bool LeyoLaVista => Programacion.Requests.Any(r => r.Metodo == Query && r.Ruta == RutaVista);

    public static EntornoDeActoPorGrupo Crear(
        string verbo,
        IReadOnlyList<object> fichas,
        string vistaJson = "{\"elementos\":[],\"siguienteCursor\":null}",
        HttpStatusCode statusVista = HttpStatusCode.OK,
        Func<string, HttpResponseMessage>? respuestaActo = null,
        IReadOnlyList<string>? codigos = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(Query, RutaVista, statusVista, vistaJson);
        foreach (var codigo in codigos ?? ["C1", "C2", "C3", "AR01", "AR02"])
            programacion.Responde(
                HttpMethod.Post, RutaActo(codigo, verbo),
                (_, _) => respuestaActo?.Invoke(codigo) ?? new HttpResponseMessage(HttpStatusCode.NoContent));

        var (clienteSedes, _) = ClienteFalso.ConFuncion(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/SUBA", StringComparison.Ordinal)
                ? ColaboradoresFalso.Json(HttpStatusCode.OK, Fixtures.Leer("SolicitarProgramacionTurno", "sede.json"))
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var colaboradores = new ColaboradoresFalso(fichas);
        var apiColaboradores = new ColaboradoresApi(
            new HttpClient(colaboradores) { BaseAddress = new Uri("https://dominio.falso.local") });

        return new EntornoDeActoPorGrupo
        {
            Programacion = programacion,
            Colaboradores = colaboradores,
            Api = new ProgramacionApi(clienteProgramacion),
            ResolutorSedes = new ResolutorSedePorCodigo(new SedesApi(clienteSedes)),
            ResolutorCandidatos = new ResolutorCandidatosPorGrupo(apiColaboradores)
        };
    }

    public static string Vista(params (string Codigo, string Nombre, string[] Fechas)[] colaboradores) =>
        "{\"elementos\":[" + string.Join(",", colaboradores.Select(c =>
            $"{{\"codigoColaborador\":\"{c.Codigo}\",\"nombreCompleto\":\"{c.Nombre}\",\"dias\":["
            + string.Join(",", c.Fechas.Select(f => $"{{\"fecha\":\"{f}\"}}")) + "]}")) + "],\"siguienteCursor\":null}";
}
