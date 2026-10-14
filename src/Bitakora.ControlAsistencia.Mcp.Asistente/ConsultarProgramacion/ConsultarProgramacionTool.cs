using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarProgramacion;

// UNA sola tool de rango sobre QUERY control-horas/turnos-vigentes (tools consolidadas):
// el caso puntual "que le toca a Juan el 3" es desde = hasta + codigo_colaborador, sin tool aparte sobre ObtenerTurnoVigente.
//
// Remodelado: se podan el Id (stream key "cd:...") y el HorarioResumido (los bloques compactos son
// la forma canonica y traen la sede); las fechas invalidas y el rango invertido se responden como
// mensaje sin llamar al dominio; el 422 upstream (rango que el propio dominio rechaza) se traduce
// pasando su mensaje.
public partial class ConsultarProgramacionTool(
    ControlHorasApi api, ResolutorCandidatosPorGrupo grupo, ResolutorSedePorCodigo sedes)
{
    internal const string NombreTool = "consultar_programacion";
    internal const int MaximoDias = 50;

    [Function("ConsultarProgramacion")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Consulta que turno rige a cada colaborador en un rango de fechas (la programacion "
            + "vigente). Filtra opcionalmente por colaborador o por sede. Para un dia puntual usa "
            + "desde = hasta.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Fecha inicial del rango, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Fecha final del rango (inclusive), formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("codigos_colaborador", "Codigos de colaborador separados por coma.")]
        string? codigosColaborador,
        [McpToolProperty("sede", "Codigo de la sede para ver a sus colaboradores.")]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.")]
        string? etiquetas,
        [McpToolProperty("sede_trabajo", "Codigo de la sede donde se trabaja.")]
        string? sedeTrabajo,
        [McpToolProperty("cursor", "El siguienteCursor de la respuesta anterior.")]
        string? cursor,
        CancellationToken ct)
    {
        _ = (api, grupo, sedes);
        throw new NotImplementedException();
    }

    private static string Compactar(BloqueVigente bloque)
    {
        var texto = new StringBuilder();

        if (bloque.Tipo == TipoBloque.Descanso)
            texto.Append("descanso ");
        else if (bloque.Tipo == TipoBloque.Extra)
            texto.Append("extra ");

        texto.Append($"{bloque.Inicio:HH\\:mm}-{bloque.Fin:HH\\:mm}");

        var dias = (bloque.Fin.Date - bloque.Inicio.Date).Days;
        if (dias > 0)
            texto.Append($"(+{dias})");

        var sede = bloque.NombreSede ?? bloque.SedeId;
        if (sede is not null)
            texto.Append($", sede: {sede}");

        return texto.ToString();
    }

    private static bool TryParseFecha(string valor, out DateOnly fecha) =>
        DateOnly.TryParseExact(valor?.Trim() ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out fecha);

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// Contrato de respuesta de consultar_programacion hacia el asistente (ventana temporal, CA-ADR-0039).
/// Desde/Hasta son los aplicados por el dominio. Sin total.
/// </summary>
public sealed record ProgramacionVigente(
    DateOnly Desde,
    DateOnly Hasta,
    int Mostrando,
    IReadOnlyList<DiaProgramado> Dias,
    string? SiguienteCursor,
    string? Nota);

/// <summary>Un dia de la programacion de un colaborador, con sus bloques ya compactados.</summary>
public sealed record DiaProgramado(
    string Colaborador,
    string? Nombre,
    DateOnly Fecha,
    string Turno,
    IReadOnlyList<string> Bloques);
