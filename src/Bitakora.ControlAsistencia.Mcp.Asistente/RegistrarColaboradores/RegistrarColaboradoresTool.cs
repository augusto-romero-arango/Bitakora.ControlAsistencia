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
// obligatoria en cada fila y nunca se completa con hoy (doctrina bitemporal, issue #574). Un 409 es
// "ya registrado", no un fallo.
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
            filas.Select((f, i) => (Fila: f ?? new FilaColaborador(), Posicion: i + 1)),
            new ParallelOptions { MaxDegreeOfParallelism = PostsSimultaneos, CancellationToken = ct },
            async (item, token) =>
            {
                var (fila, posicion) = item;
                var identificacion = fila.Identificacion();

                if (Validar(fila) is { } motivoInvalido)
                {
                    rechazados.Enqueue(new FilaRechazada(posicion, identificacion, motivoInvalido));
                    return;
                }

                var fecha = DateOnly.ParseExact(fila.FechaInicio!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var respuesta = await api.Registrar(
                    new RegistroColaboradorSolicitado(
                        fila.TipoIdentificacion!, fila.NumeroIdentificacion!, fila.PrimerNombre!,
                        fila.SegundoNombre, fila.PrimerApellido!, fila.SegundoApellido,
                        fila.CodigoColaborador!, fecha, fila.CodigoSede),
                    token);

                switch (respuesta.StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        yaRegistrados.Enqueue((posicion, identificacion!));
                        break;
                    case HttpStatusCode.BadRequest:
                        rechazados.Enqueue(new FilaRechazada(
                            posicion,
                            identificacion,
                            string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(token))));
                        break;
                    default:
                        if (respuesta.IsSuccessStatusCode)
                            Interlocked.Increment(ref registrados);
                        else
                            rechazados.Enqueue(new FilaRechazada(
                                posicion,
                                identificacion,
                                string.Format(Mensajes.RechazoDelDominio, await respuesta.Content.ReadAsStringAsync(token))));
                        break;
                }
            });

        var ya = yaRegistrados.OrderBy(y => y.Fila).Select(y => y.Identificacion).ToList();
        var rech = rechazados.OrderBy(r => r.Fila).ToList();

        return RespuestaJson.Serializar(new ColaboradoresRegistradosResumen(
            Mensajes.ResultadoColaboradoresRegistrados,
            registrados,
            ya.Count > 0 ? ya : null,
            rech.Count > 0 ? rech : null,
            Mensajes.NotaVisibilidadEventual));
    }

    private static string? Validar(FilaColaborador fila)
    {
        var obligatorios = new (string Nombre, string? Valor)[]
        {
            ("tipo_identificacion", fila.TipoIdentificacion),
            ("numero_identificacion", fila.NumeroIdentificacion),
            ("primer_nombre", fila.PrimerNombre),
            ("primer_apellido", fila.PrimerApellido),
            ("codigo_colaborador", fila.CodigoColaborador),
            ("fecha_inicio", fila.FechaInicio)
        };

        if (obligatorios.FirstOrDefault(o => string.IsNullOrWhiteSpace(o.Valor)) is { Nombre: not null } vacio)
            return string.Format(Mensajes.CampoObligatorio, vacio.Nombre);

        return DateOnly.TryParseExact(
            fila.FechaInicio, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : string.Format(Mensajes.FechaInvalida, fila.FechaInicio);
    }
}

public sealed class FilaColaborador
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
}

public sealed record FilaRechazada(int Fila, string? Identificacion, string Motivo);

public sealed record ColaboradoresRegistradosResumen(
    string Resultado,
    int Registrados,
    [property: JsonPropertyName("ya_registrados")] IReadOnlyList<string>? YaRegistrados,
    IReadOnlyList<FilaRechazada>? Rechazados,
    string Nota);
