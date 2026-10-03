using System.Collections.Concurrent;
using System.Globalization;
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
    private readonly ResolutorSedePorCodigo resolutorSedes = new(sedes);

    [Function("SolicitarProgramacionTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Programa un turno a una lista de colaboradores en una sede, para todos los dias de "
            + "una ventana de trabajo de maximo 35 dias. Recibe la ventana (desde, hasta), el "
            + "nombre exacto del turno del catalogo (miralo con listar_turnos), el codigo de la "
            + "sede donde se registrara la programacion -- sede de programacion, distinta de la "
            + "sede de trabajo de cada colaborador; pidesela al usuario, nunca la asumas -- y las "
            + "identificaciones completas de los colaboradores, separadas por coma, tal como las "
            + "devuelven buscar_colaboradores o listar_colaboradores: no las inventes ni pases "
            + "numeros sin tipo. A cada colaborador le programa solo los dias de la ventana que su "
            + "vinculacion cubre; los que no cubren ninguno o no se encuentran se omiten sin "
            + "detalle. Respeta las ausencias de cada colaborador: la respuesta dice cuales dias se "
            + "respetaron y por que. Responde quienes quedaron programados y con que fechas. La "
            + "programacion aparece en consultar_programacion unos segundos despues.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "desde",
            "Primer dia de la ventana de trabajo, formato yyyy-MM-dd. La ventana no puede pasar "
            + "de 35 dias.",
            isRequired: true)]
        string desde,
        [McpToolProperty(
            "hasta",
            "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd. La ventana no puede pasar "
            + "de 35 dias.",
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

        // El mensaje de la tool incluye el conteo; el VO solo valida su invariante (MEF-ADR-0012).
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

        var resolucionSede = await resolutorSedes.ResolverAsync(sedeDeProgramacion, ct);
        if (resolucionSede.FalloDeLectura is { } falloSede)
            return string.Format(Mensajes.RechazoDelDominio, falloSede);
        if (resolucionSede.MensajeDelMotivo(
            sedeDeProgramacion, noExiste: Mensajes.SedeNoExiste, inactiva: Mensajes.SedeInactiva) is { } rechazo)
            return rechazo;
        var sedeProgramada = resolucionSede.Sede!;

        var respuestaDirectorio = await colaboradores.BuscarEnDirectorio(
            identificacionesSolicitadas, MaximoIdentificaciones, ct);
        if (await respuestaDirectorio.LeerFalloAsync(ct) is { } falloDirectorio)
            return string.Format(Mensajes.RechazoDelDominio, falloDirectorio);
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
                    var respetadas = await LeerRespetadasAsync(respuestaSolicitud, tokenInterno);
                    var fechasRespetadas = respetadas.Select(r => r.Fecha).ToHashSet();
                    var diasProgramados = candidato.Dias.Count(d => !fechasRespetadas.Contains(d));
                    var respetados = respetadas
                        .Where(r => candidato.Dias.Contains(r.Fecha))
                        .GroupBy(r => r.Motivo)
                        .OrderBy(g => g.Key, StringComparer.Ordinal)
                        .Select(g => new DiasRespetadosResumen(
                            g.Key, ComprimirEnTramos(g.Select(r => r.Fecha), candidato.Dias[0])))
                        .ToList();

                    programados.Add(new ColaboradorProgramadoResumen(
                        candidato.Entrada.Identificacion,
                        candidato.Entrada.NombreCompleto,
                        candidato.Entrada.CodigoColaborador,
                        candidato.Dias[0],
                        candidato.Dias[^1],
                        diasProgramados,
                        respetados.Count == 0 ? null : respetados));
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
            new SedeResumen(sedeProgramada.Id, sedeProgramada.Nombre),
            ventana.ToString(),
            [.. programados.OrderBy(p => p.Identificacion, StringComparer.Ordinal)],
            omitidos,
            fallidos.IsEmpty ? null : [.. fallidos.OrderBy(f => f.Identificacion, StringComparer.Ordinal)],
            Mensajes.NotaVisibilidadEventual));
    }

    private static async Task<IReadOnlyList<FechaRespetadaPorAusencia>> LeerRespetadasAsync(
        HttpResponseMessage respuesta, CancellationToken ct)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(cuerpo))
            return [];

        try
        {
            return JsonSerializer.Deserialize<RespuestaSolicitudCreada>(cuerpo, OpcionesLectura)
                ?.FechasRespetadas ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // Los extremos van como dia del mes cuando caen en el mes de referencia (primer dia
    // programado del colaborador) y como yyyy-MM-dd cuando salen de el; en ese caso el tramo se
    // une con " a " porque "30-2026-10-01" no se lee como rango.
    private static string ComprimirEnTramos(IEnumerable<DateOnly> fechas, DateOnly referencia)
    {
        bool EnMesDeReferencia(DateOnly f) => f.Year == referencia.Year && f.Month == referencia.Month;
        string Formato(DateOnly f) => EnMesDeReferencia(f)
            ? f.Day.ToString(CultureInfo.InvariantCulture)
            : f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string Rango(DateOnly primero, DateOnly ultimo) =>
            EnMesDeReferencia(primero) && EnMesDeReferencia(ultimo)
                ? $"{Formato(primero)}-{Formato(ultimo)}"
                : $"{Formato(primero)} a {Formato(ultimo)}";

        var ordenadas = fechas.Distinct().OrderBy(f => f).ToList();
        var tramos = new List<string>();
        var inicio = 0;
        for (var i = 1; i <= ordenadas.Count; i++)
        {
            if (i < ordenadas.Count && ordenadas[i].DayNumber == ordenadas[i - 1].DayNumber + 1)
                continue;

            var primero = ordenadas[inicio];
            var ultimo = ordenadas[i - 1];
            tramos.Add(primero == ultimo ? Formato(primero) : Rango(primero, ultimo));
            inicio = i;
        }

        return string.Join(", ", tramos);
    }
}

internal sealed record RespuestaSolicitudCreada(IReadOnlyList<FechaRespetadaPorAusencia>? FechasRespetadas);

internal sealed record FechaRespetadaPorAusencia(DateOnly Fecha, string Motivo);

/// <summary>
/// Eco compacto de solicitar_programacion_turno hacia el asistente: cada solicitud del dominio
/// responde 201 con solo las fechas respetadas por ausencia, asi que el hecho programado se
/// reconstruye con lo que entro a la tool, lo que devolvio el directorio y esas fechas.
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
    string Identificacion, string Nombre, string CodigoColaborador, DateOnly Desde, DateOnly Hasta, int Dias,
    IReadOnlyList<DiasRespetadosResumen>? Respetados = null);

public sealed record DiasRespetadosResumen(string Motivo, string Tramos);

public sealed record ColaboradorFallidoResumen(string Identificacion, string Motivo);
