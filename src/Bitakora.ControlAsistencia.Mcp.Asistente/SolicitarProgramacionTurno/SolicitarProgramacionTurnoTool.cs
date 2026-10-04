using System.Globalization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

// Cliente HTTP puro que envuelve N veces el comando SolicitarProgramacionTurno (POST
// programacion/solicitudes): un lote no es concepto del dominio (glosario: Ventana de trabajo es
// efimera, nunca se persiste) y el marco no ofrece atomicidad entre streams -- por eso esta tool
// re-verifica sede/turno/directorio por su cuenta y arma una solicitud por colaborador (MEF-ADR-0047
// decision 4). Los rechazos del dominio en cada POST se traducen a texto (CA-ADR-0030) y no
// detienen al resto del lote: el resto ya pudo haberse programado.
public partial class SolicitarProgramacionTurnoTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores,
    ILogger<SolicitarProgramacionTurnoTool>? logger = null, TimeProvider? reloj = null)
{
    private readonly ILogger registro = (ILogger?)logger ?? NullLogger.Instance;
    private readonly TimeProvider relojDeEjecucion = reloj ?? TimeProvider.System;

    internal const string NombreTool = "solicitar_programacion_turno";

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);
    private readonly ResolutorSedePorCodigo resolutorSedes = new(sedes);
    private readonly ResolutorCandidatosPorLista resolutorCandidatos = new(colaboradores);

    [Function("SolicitarProgramacionTurno")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Programa un turno a una lista de colaboradores en una sede (la de programacion, opcional), "
            + "para todos los dias de una ventana de trabajo de maximo 35 dias. Recibe la ventana "
            + "(desde, hasta), el nombre exacto del turno del catalogo (miralo con listar_turnos), "
            + "opcionalmente el codigo de la sede de programacion -- distinta de la sede de trabajo de "
            + "cada colaborador; sugiere una al usuario y, si prefiere la sede de cada colaborador, "
            + "omite el parametro -- y las identificaciones completas de los colaboradores, separadas por coma, tal como las "
            + "devuelven buscar_colaboradores o listar_colaboradores: no las inventes ni pases "
            + "numeros sin tipo. A cada colaborador le programa solo los dias de la ventana que su "
            + "vinculacion cubre; los que no cubren ninguno o no se encuentran se omiten sin "
            + "detalle. Respeta las ausencias de cada colaborador: la respuesta dice cuales dias se "
            + "respetaron y por que. Responde quienes quedaron programados y con que fechas. La "
            + "programacion aparece en consultar_programacion unos segundos despues. Para programar a todo "
            + "un grupo por sede o etiquetas usa solicitar_programacion_turno_por_grupo.")]
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
            "Opcional. Codigo de la sede donde se registra la programacion; sugierela al usuario. "
            + "Si prefiere la de cada colaborador, omitela: se usa la sede de trabajo de cada uno "
            + "para las franjas sin sede prearmada. No es la sede de trabajo del colaborador.",
            isRequired: false)]
        string? sedeDeProgramacion,
        [McpToolProperty(
            "identificaciones",
            "Identificaciones completas separadas por coma ('CC-79879078, CE-887766'), tal como "
            + "las devuelve buscar_colaboradores.",
            isRequired: true)]
        string identificaciones,
        CancellationToken ct)
    {
        var inicio = relojDeEjecucion.GetTimestamp();
        if (string.IsNullOrWhiteSpace(desde))
            return string.Format(Mensajes.CampoObligatorio, "desde");
        if (string.IsNullOrWhiteSpace(hasta))
            return string.Format(Mensajes.CampoObligatorio, "hasta");
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");
        if (string.IsNullOrWhiteSpace(identificaciones))
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

        var identificacionesSolicitadas = identificaciones
            .Split(',')
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToList();
        if (identificacionesSolicitadas.Count == 0)
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

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

        SedeProgramada? sedeExplicita = null;
        if (!string.IsNullOrWhiteSpace(sedeDeProgramacion))
        {
            var resolucionSede = await resolutorSedes.ResolverAsync(sedeDeProgramacion, ct);
            if (resolucionSede.FalloDeLectura is { } falloSede)
                return string.Format(Mensajes.RechazoDelDominio, falloSede);
            if (resolucionSede.MensajeDelMotivo(
                sedeDeProgramacion, noExiste: Mensajes.SedeNoExiste, inactiva: Mensajes.SedeInactiva) is { } rechazo)
                return rechazo;
            sedeExplicita = resolucionSede.Sede!;
        }

        var (planDeSede, falloMaestro) = await PlanDeSede.CrearAsync(
            sedes, sedeExplicita, fichaTurno,
            new MotivosDeAviso(Mensajes.AvisoSinSede, Mensajes.AvisoSedeInactiva, Mensajes.AvisoSedeNoExiste), ct);
        if (falloMaestro is not null)
            return string.Format(Mensajes.RechazoDelDominio, falloMaestro);

        var resolucionCandidatos = await resolutorCandidatos.ResolverAsync(identificacionesSolicitadas, ct);
        if (resolucionCandidatos.FalloDeLectura is { } falloDirectorio)
            return string.Format(Mensajes.RechazoDelDominio, falloDirectorio);
        var solicitados = resolucionCandidatos.Candidatos;

        var omitidosPorDirectorio = identificacionesSolicitadas.Count - solicitados.Count;
        var contadores = new ContadoresDeEjecucion { Omitidos = omitidosPorDirectorio };
        ResultadoEjecucion ejecucion;
        try
        {
            ejecucion = await EjecutorDeProgramacion.EjecutarAsync(
                programacion, solicitados, Guid.Parse(fichaTurno.Id), planDeSede!, ventana, contadores, ct);
        }
        finally
        {
            IndicadorDeEjecucion.Emitir(
                registro, relojDeEjecucion, inicio,
                new DatosDeIndicador(
                    "lista", solicitados.Count, fichaTurno.Nombre, sedeExplicita?.Id,
                    fechaDesde, fechaHasta, null, null),
                contadores);
        }

        return RespuestaJson.Serializar(new ProgramacionSolicitadaResumen(
            Mensajes.ResultadoProgramacionSolicitada,
            fichaTurno.Nombre,
            sedeExplicita is null ? null : new SedeResumen(sedeExplicita.Id, sedeExplicita.Nombre),
            ventana.ToString(),
            ejecucion.Programados,
            ejecucion.Omitidos + omitidosPorDirectorio,
            ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual,
            ejecucion.Avisos));
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
    SedeResumen? Sede,
    string Ventana,
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    string Nota,
    IReadOnlyList<AvisoDeSede>? Avisos = null);

public sealed record SedeResumen(string Codigo, string Nombre);

public sealed record ColaboradorProgramadoResumen(
    string Identificacion, string Nombre, string CodigoColaborador, DateOnly Desde, DateOnly Hasta, int Dias,
    IReadOnlyList<DiasRespetadosResumen>? Respetados = null, string? Sede = null);

public sealed record DiasRespetadosResumen(string Motivo, string Tramos);

public sealed record ColaboradorFallidoResumen(string Identificacion, string Motivo, string? Turno = null);
