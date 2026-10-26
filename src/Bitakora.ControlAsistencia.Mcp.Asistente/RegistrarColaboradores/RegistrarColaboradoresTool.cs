using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.RegistrarColaboradores;

// Orquesta N POST de RegistrarColaborador sin inventar un aggregate de lote. fecha_inicio es
// obligatoria en cada fila y nunca se completa con hoy (doctrina bitemporal). Un 409 es "ya
// registrado", no un fallo.
public partial class RegistrarColaboradoresTool(ColaboradoresApi api)
{
    internal const string NombreTool = "registrar_colaboradores";
    internal const int PostsSimultaneos = 8;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    [Function("RegistrarColaboradores")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Pone bajo control de asistencia una lista de personas: por cada una crea el "
            + "colaborador y abre su vinculacion desde su fecha_inicio. Cada fila lleva los mismos "
            + "campos que registrar_colaborador; fecha_inicio es obligatoria en cada fila "
            + "(pidesela al usuario: nunca la asumas). Un rechazo no detiene a los demas.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "colaboradores",
            "Lista en JSON de objetos con tipo_identificacion, numero_identificacion, primer_nombre, "
            + "segundo_nombre (opcional), primer_apellido, segundo_apellido (opcional), "
            + "codigo_colaborador, fecha_inicio (yyyy-MM-dd) y codigo_sede (opcional). Mismos "
            + "nombres y reglas que registrar_colaborador.",
            isRequired: true)]
        string colaboradores,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(colaboradores))
            return Mensajes.ColaboradoresObligatorio;

        List<FilaColaborador?>? filas;
        try
        {
            filas = JsonSerializer.Deserialize<List<FilaColaborador?>>(colaboradores, OpcionesLectura);
        }
        catch (JsonException)
        {
            return Mensajes.ColaboradoresJsonInvalido;
        }

        if (filas is null)
            return Mensajes.ColaboradoresJsonInvalido;

        if (filas.Count == 0)
            return Mensajes.ColaboradoresListaVacia;

        var registrados = 0;
        var yaRegistrados = new ConcurrentQueue<(int Fila, string Identificacion)>();
        var rechazados = new ConcurrentQueue<FilaRechazada>();

        await Parallel.ForEachAsync(
            filas.Select((fila, i) => (Fila: fila ?? new FilaColaborador(), Posicion: i + 1)),
            new ParallelOptions { MaxDegreeOfParallelism = PostsSimultaneos, CancellationToken = ct },
            async (item, token) =>
            {
                var (fila, posicion) = item;
                var (solicitud, motivoInvalido) = fila.ASolicitud();
                if (solicitud is null)
                {
                    rechazados.Enqueue(new FilaRechazada(posicion, fila.Identificacion(), motivoInvalido!));
                    return;
                }

                var identificacion = $"{solicitud.TipoIdentificacion}-{solicitud.NumeroIdentificacion}";
                var respuesta = await api.Registrar(solicitud, token);
                if (respuesta.StatusCode == HttpStatusCode.Conflict)
                    yaRegistrados.Enqueue((posicion, identificacion));
                else if (await respuesta.LeerFalloAsync(token) is { } fallo)
                    rechazados.Enqueue(new FilaRechazada(
                        posicion, identificacion, string.Format(Mensajes.RechazoDelDominio, fallo)));
                else
                    Interlocked.Increment(ref registrados);
            });

        List<string> ya = [.. yaRegistrados.OrderBy(y => y.Fila).Select(y => y.Identificacion)];
        List<FilaRechazada> rech = [.. rechazados.OrderBy(r => r.Fila)];

        return RespuestaJson.Serializar(new ColaboradoresRegistradosResumen(
            Mensajes.ResultadoColaboradoresRegistrados,
            registrados,
            ya.Count > 0 ? ya : null,
            rech.Count > 0 ? rech : null,
            Mensajes.NotaVisibilidadEventual));
    }
}

internal sealed class FilaColaborador
{
    [JsonPropertyName("tipo_identificacion")] public string? TipoIdentificacion { get; init; }
    [JsonPropertyName("numero_identificacion")] public string? NumeroIdentificacion { get; init; }
    [JsonPropertyName("primer_nombre")] public string? PrimerNombre { get; init; }
    [JsonPropertyName("segundo_nombre")] public string? SegundoNombre { get; init; }
    [JsonPropertyName("primer_apellido")] public string? PrimerApellido { get; init; }
    [JsonPropertyName("segundo_apellido")] public string? SegundoApellido { get; init; }
    [JsonPropertyName("codigo_colaborador")] public string? CodigoColaborador { get; init; }
    [JsonPropertyName("fecha_inicio")] public string? FechaInicio { get; init; }
    [JsonPropertyName("codigo_sede")] public string? CodigoSede { get; init; }

    public string? Identificacion() =>
        string.IsNullOrWhiteSpace(TipoIdentificacion) || string.IsNullOrWhiteSpace(NumeroIdentificacion)
            ? null
            : $"{TipoIdentificacion}-{NumeroIdentificacion}";

    public (RegistroColaboradorSolicitado? Solicitud, string? Motivo) ASolicitud()
    {
        if (string.IsNullOrWhiteSpace(TipoIdentificacion))
            return Obligatorio("tipo_identificacion");
        if (string.IsNullOrWhiteSpace(NumeroIdentificacion))
            return Obligatorio("numero_identificacion");
        if (string.IsNullOrWhiteSpace(PrimerNombre))
            return Obligatorio("primer_nombre");
        if (string.IsNullOrWhiteSpace(PrimerApellido))
            return Obligatorio("primer_apellido");
        if (string.IsNullOrWhiteSpace(CodigoColaborador))
            return Obligatorio("codigo_colaborador");
        if (string.IsNullOrWhiteSpace(FechaInicio))
            return Obligatorio("fecha_inicio");

        if (!DateOnly.TryParseExact(
                FechaInicio, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return (null, string.Format(RegistrarColaboradoresTool.Mensajes.FechaInvalida, FechaInicio));

        return (new RegistroColaboradorSolicitado(
            TipoIdentificacion, NumeroIdentificacion, PrimerNombre, SegundoNombre,
            PrimerApellido, SegundoApellido, CodigoColaborador, fecha, CodigoSede), null);
    }

    private static (RegistroColaboradorSolicitado?, string?) Obligatorio(string campo) =>
        (null, string.Format(RegistrarColaboradoresTool.Mensajes.CampoObligatorio, campo));
}

public sealed record FilaRechazada(int Fila, string? Identificacion, string Motivo);

public sealed record ColaboradoresRegistradosResumen(
    string Resultado,
    int Registrados,
    [property: JsonPropertyName("ya_registrados")] IReadOnlyList<string>? YaRegistrados,
    IReadOnlyList<FilaRechazada>? Rechazados,
    string Nota);
