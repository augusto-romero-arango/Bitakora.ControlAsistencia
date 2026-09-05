using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;

// Cliente HTTP puro que envuelve N veces el comando SolicitarProgramacionTurno (POST
// programacion/solicitudes): un lote no es concepto del dominio (glosario: Ventana de trabajo es
// efimera, nunca se persiste) y el marco no ofrece atomicidad entre streams -- por eso esta tool
// re-verifica sede/turno/directorio por su cuenta y arma una solicitud por colaborador (MEF-ADR-0047
// decision 4). Los rechazos del dominio en cada POST se traducen a texto (CA-ADR-0030) y no
// detienen al resto del lote: el resto ya pudo haberse programado.
public partial class SolicitarProgramacionTurnoTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores)
{
    internal const string NombreTool = "solicitar_programacion_turno";
    internal const int MaximoIdentificaciones = 200;
    internal const int PostsSimultaneos = 8;

    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);

    [Function("SolicitarProgramacionTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Programa un turno a una lista de colaboradores en una sede, para todos los dias de "
            + "una ventana de trabajo de maximo 31 dias. Recibe la ventana (desde, hasta), el "
            + "nombre exacto del turno del catalogo (miralo con listar_turnos), el codigo de la "
            + "sede donde se registrara la programacion -- sede de programacion, distinta de la "
            + "sede de trabajo de cada colaborador; pidesela al usuario, nunca la asumas -- y las "
            + "identificaciones completas de los colaboradores, separadas por coma, tal como las "
            + "devuelven buscar_colaboradores o listar_colaboradores: no las inventes ni pases "
            + "numeros sin tipo. A cada colaborador le programa solo los dias de la ventana que su "
            + "vinculacion cubre; los que no cubren ninguno o no se encuentran se omiten sin "
            + "detalle. Responde quienes quedaron programados y con que fechas. La programacion "
            + "aparece en consultar_programacion unos segundos despues.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "desde",
            "Primer dia de la ventana de trabajo, formato yyyy-MM-dd. La ventana no puede pasar "
            + "de 31 dias.",
            isRequired: true)]
        string desde,
        [McpToolProperty(
            "hasta",
            "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd. La ventana no puede pasar "
            + "de 31 dias.",
            isRequired: true)]
        string hasta,
        [McpToolProperty(
            "turno",
            "Nombre exacto del turno del catalogo (ej. 'Cocina manana'); miralo con listar_turnos.",
            isRequired: true)]
        string turno,
        [McpToolProperty(
            "sede_de_programacion",
            "Codigo de la sede donde se registra la programacion. Pidesela al usuario; no es la "
            + "sede de trabajo del colaborador.",
            isRequired: true)]
        string sedeDeProgramacion,
        [McpToolProperty(
            "identificaciones",
            "Identificaciones completas separadas por coma ('CC-79879078, CE-887766'), tal como "
            + "las devuelve buscar_colaboradores; maximo 200.",
            isRequired: true)]
        string identificaciones,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(desde))
            return string.Format(Mensajes.CampoObligatorio, "desde");
        if (string.IsNullOrWhiteSpace(hasta))
            return string.Format(Mensajes.CampoObligatorio, "hasta");
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");
        if (string.IsNullOrWhiteSpace(sedeDeProgramacion))
            return string.Format(Mensajes.CampoObligatorio, "sede_de_programacion");
        if (string.IsNullOrWhiteSpace(identificaciones))
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

        var identificacionesSolicitadas = identificaciones
            .Split(',')
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToList();
        if (identificacionesSolicitadas.Count == 0)
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");
        if (identificacionesSolicitadas.Count > MaximoIdentificaciones)
            return string.Format(Mensajes.DemasiadasIdentificaciones, MaximoIdentificaciones);

        if (!DateOnly.TryParseExact(
            desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaDesde))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);
        if (!DateOnly.TryParseExact(
            hasta, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaHasta))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);

        // La ventana formatea su propio mensaje con el conteo de dias (no publico en el VO: su
        // Crear solo asegura el invariante, MEF-ADR-0012) antes de delegarle la construccion.
        if (fechaDesde > fechaHasta)
            return Mensajes.VentanaInvertida;

        var diasVentana = (fechaHasta.DayNumber - fechaDesde.DayNumber) + 1;
        if (diasVentana > VentanaDeProgramacion.MaximoDias)
            return string.Format(Mensajes.VentanaExcedeMaximo, diasVentana);

        var ventana = VentanaDeProgramacion.Crear(fechaDesde, fechaHasta);

        var resolucion = await resolutor.ResolverAsync(turno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.TurnoNoExiste, turno, string.Join(", ", resolucion.NombresDisponibles));
        var fichaTurno = resolucion.Ficha;

        var respuestaSede = await sedes.ObtenerFicha(sedeDeProgramacion, ct);
        if (respuestaSede.StatusCode == HttpStatusCode.NotFound)
            return string.Format(Mensajes.SedeNoExiste, sedeDeProgramacion);
        if (await TraducirFalloDeLectura(respuestaSede, ct) is { } falloSede)
            return falloSede;
        var fichaSede = (await respuestaSede.Content.ReadFromJsonAsync<FichaSede>(OpcionesLectura, ct))!;
        if (!fichaSede.Activa)
            return string.Format(Mensajes.SedeInactiva, sedeDeProgramacion);

        var respuestaDirectorio = await colaboradores.BuscarEnDirectorio(
            identificacionesSolicitadas, MaximoIdentificaciones, ct);
        if (await TraducirFalloDeLectura(respuestaDirectorio, ct) is { } falloDirectorio)
            return falloDirectorio;
        var directorio = await respuestaDirectorio.Content.ReadFromJsonAsync<List<EntradaDirectorio>>(OpcionesLectura, ct) ?? [];

        var identificacionesNormalizadas = identificacionesSolicitadas
            .Select(i => i.ToUpperInvariant())
            .ToHashSet();

        var candidatos = directorio
            .Where(entrada => identificacionesNormalizadas.Contains(entrada.Identificacion.Trim().ToUpperInvariant()))
            .Select(entrada => (
                Entrada: entrada,
                Dias: ventana.DiasCubiertosPor(entrada.VigenteDesde, entrada.VigenteHasta)))
            .Where(candidato => candidato.Dias.Count > 0)
            .ToList();

        var omitidos = identificacionesSolicitadas.Count - candidatos.Count;
        var turnoId = Guid.Parse(fichaTurno.Id);
        var sedeProgramada = new SedeProgramada(fichaSede.Codigo, fichaSede.Nombre, fichaSede.CentroDeCostos);

        var programados = new ConcurrentBag<ColaboradorProgramadoResumen>();
        var fallidos = new ConcurrentBag<ColaboradorFallidoResumen>();

        await Parallel.ForEachAsync(
            candidatos,
            new ParallelOptions { MaxDegreeOfParallelism = PostsSimultaneos, CancellationToken = ct },
            async (candidato, tokenInterno) =>
            {
                var solicitud = new SolicitudProgramacionTurno(
                    Guid.CreateVersion7(),
                    turnoId,
                    new ColaboradorSolicitado(
                        candidato.Entrada.Identificacion,
                        candidato.Entrada.CodigoColaborador,
                        candidato.Entrada.NombreCompleto),
                    candidato.Dias,
                    sedeProgramada);

                var respuestaSolicitud = await programacion.SolicitarProgramacion(solicitud, tokenInterno);

                if (respuestaSolicitud.IsSuccessStatusCode)
                {
                    programados.Add(new ColaboradorProgramadoResumen(
                        candidato.Entrada.Identificacion,
                        candidato.Entrada.NombreCompleto,
                        candidato.Entrada.CodigoColaborador,
                        candidato.Dias[0],
                        candidato.Dias[^1],
                        candidato.Dias.Count));
                }
                else
                {
                    var motivo = await respuestaSolicitud.Content.ReadAsStringAsync(tokenInterno);
                    fallidos.Add(new ColaboradorFallidoResumen(candidato.Entrada.Identificacion, motivo));
                }
            });

        return RespuestaJson.Serializar(new ProgramacionSolicitadaResumen(
            Mensajes.ResultadoProgramacionSolicitada,
            fichaTurno.Nombre,
            new SedeResumen(fichaSede.Codigo, fichaSede.Nombre),
            ventana.ToString(),
            [.. programados.OrderBy(p => p.Identificacion, StringComparer.Ordinal)],
            omitidos,
            fallidos.IsEmpty ? null : [.. fallidos.OrderBy(f => f.Identificacion, StringComparer.Ordinal)],
            Mensajes.NotaVisibilidadEventual));
    }

    // Las tres lecturas previas (catalogo, ficha de sede, directorio) son el boundary del sistema:
    // un 5xx del dominio -- o un cuerpo que no es el JSON esperado -- llegaria como excepcion cruda
    // a la tool call. Se traduce a texto como cualquier otro rechazo (CA-ADR-0030), con el status
    // cuando el cuerpo viene vacio (un 503 de un Function App frio no trae body).
    private static async Task<string?> TraducirFalloDeLectura(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return null;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        return string.Format(
            Mensajes.RechazoDelDominio,
            string.IsNullOrWhiteSpace(cuerpo) ? ((int)respuesta.StatusCode).ToString() : cuerpo);
    }
}

/// <summary>
/// Eco compacto de solicitar_programacion_turno hacia el asistente: cada solicitud del dominio
/// responde 202 sin body, asi que el hecho programado se reconstruye con lo que entro a la tool y
/// lo que devolvio el directorio.
/// </summary>
public sealed record ProgramacionSolicitadaResumen(
    string Resultado,
    string Turno,
    SedeResumen Sede,
    string Ventana,
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    string Nota);

public sealed record SedeResumen(string Codigo, string Nombre);

public sealed record ColaboradorProgramadoResumen(
    string Identificacion, string Nombre, string CodigoColaborador, DateOnly Desde, DateOnly Hasta, int Dias);

public sealed record ColaboradorFallidoResumen(string Identificacion, string Motivo);
