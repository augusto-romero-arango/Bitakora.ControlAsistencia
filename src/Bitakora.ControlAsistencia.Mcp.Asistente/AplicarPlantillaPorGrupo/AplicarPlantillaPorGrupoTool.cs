using System.Globalization;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaPorGrupo;

public partial class AplicarPlantillaPorGrupoTool(
    ProgramacionApi programacion, SedesApi sedes,
    ResolutorPlantillaTurnosPorId resolutorPlantillas, ResolutorSedePorCodigo resolutorSedes, ResolutorCandidatosPorGrupo resolutorCandidatos,
    ILogger<AplicarPlantillaPorGrupoTool>? logger = null, TimeProvider? reloj = null)
{
    private readonly ILogger registro = (ILogger?)logger ?? NullLogger.Instance;
    private readonly TimeProvider relojDeEjecucion = reloj ?? TimeProvider.System;

    internal const string NombreTool = "aplicar_plantilla_por_grupo";

    [Function("AplicarPlantillaPorGrupo")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Aplica una plantilla de turnos a todos los colaboradores de un grupo, definido por sede y/o etiquetas, "
            + "durante una ventana de trabajo de maximo 35 dias; el ciclo empieza en fecha_inicio (dia 1) y se repite cada N dias. "
            + "fecha_inicio puede ser anterior a la ventana; para continuar una rotacion ya aplicada, usa la misma fecha_inicio "
            + "de la vez anterior (preguntala al usuario). Los dias de la ventana anteriores a fecha_inicio no se programan. "
            + "Para personas concretas usa aplicar_plantilla. Ubica la plantilla con buscar_plantillas.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("plantilla_id", "Id de la plantilla de turnos, tal como lo da buscar_plantillas.", isRequired: true)]
        string plantillaId,
        [McpToolProperty("fecha_inicio", "Fecha en que cae el dia 1 del ciclo, formato yyyy-MM-dd. Puede ser anterior a 'desde'.", isRequired: true)]
        string fechaInicio,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("sede_de_programacion", "Opcional. Codigo de la sede donde se registra la programacion. Si prefiere la de cada colaborador, omitela.", isRequired: false)]
        string? sedeDeProgramacion,
        [McpToolProperty("sede", "Codigo de la sede de trabajo de los colaboradores del grupo.", isRequired: false)]
        string? sede,
        [McpToolProperty("etiquetas", "Pares categoria:valor separados por coma.", isRequired: false)]
        string? etiquetas,
        CancellationToken ct)
    {
        var inicio = relojDeEjecucion.GetTimestamp();
        if (string.IsNullOrWhiteSpace(desde))
            return string.Format(Mensajes.CampoObligatorio, "desde");
        if (string.IsNullOrWhiteSpace(hasta))
            return string.Format(Mensajes.CampoObligatorio, "hasta");
        if (string.IsNullOrWhiteSpace(plantillaId))
            return string.Format(Mensajes.CampoObligatorio, "plantilla_id");
        if (string.IsNullOrWhiteSpace(fechaInicio))
            return string.Format(Mensajes.CampoObligatorio, "fecha_inicio");
        if (!Guid.TryParse(plantillaId, out var plantillaGuid))
            return string.Format(Mensajes.PlantillaIdInvalido, plantillaId);

        var mensajesSelector = new MensajesDeSelector(
            Mensajes.SelectorObligatorio, Mensajes.EtiquetaMalFormada, Mensajes.SedeDelSelectorNoExiste,
            Mensajes.SedeDelSelectorInactiva, Mensajes.RechazoDelDominio);
        var (selectorInterpretado, rechazoInterpretacion) = SelectorDeGrupo.Interpretar(
            sede, etiquetas, mensajesSelector);
        if (rechazoInterpretacion is not null)
            return rechazoInterpretacion;

        if (!DateOnly.TryParseExact(
            fechaInicio, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaDeInicio))
            return string.Format(Mensajes.FechaInvalida, "fecha_inicio", fechaInicio);
        if (!DateOnly.TryParseExact(
            desde, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaDesde))
            return string.Format(Mensajes.FechaInvalida, "desde", desde);
        if (!DateOnly.TryParseExact(
            hasta, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fechaHasta))
            return string.Format(Mensajes.FechaInvalida, "hasta", hasta);
        if (fechaDesde > fechaHasta)
            return Mensajes.VentanaInvertida;

        var diasVentana = (fechaHasta.DayNumber - fechaDesde.DayNumber) + 1;
        if (diasVentana > VentanaDeProgramacion.MaximoDias)
            return string.Format(Mensajes.VentanaExcedeMaximo, diasVentana);

        if (fechaDeInicio > fechaHasta)
            return string.Format(Mensajes.FechaInicioPosteriorAHasta, fechaInicio, hasta);

        var desdeEfectivo = fechaDeInicio > fechaDesde ? fechaDeInicio : fechaDesde;
        var diasAntesDelInicio = desdeEfectivo.DayNumber - fechaDesde.DayNumber;
        var ventana = VentanaDeProgramacion.Crear(desdeEfectivo, fechaHasta);

        var (moldeResuelto, rechazoPlantilla) = await MoldeDePlantillaTurnos.ResolverAsync(
            resolutorPlantillas, plantillaGuid,
            new MensajesDePlantilla(
                Mensajes.PlantillaNoExiste, Mensajes.PlantillaIncompleta, Mensajes.PlantillaConTurnoNoProgramable,
                Mensajes.RechazoDelDominio), ct);
        if (rechazoPlantilla is not null)
            return rechazoPlantilla;
        var molde = moldeResuelto!;

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

        var (selectorDeGrupo, rechazoSelector) = await selectorInterpretado!.ValidarSedeAsync(
            resolutorSedes, mensajesSelector, ct);
        if (rechazoSelector is not null)
            return rechazoSelector;

        IReadOnlySet<Guid> turnosConFranjaSinSede = new HashSet<Guid>();
        if (sedeExplicita is null)
        {
            (turnosConFranjaSinSede, var falloTurnos) = await molde.TurnosConFranjaSinSedeAsync(programacion, ct);
            if (falloTurnos is not null)
                return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        }

        var (planDeSede, falloMaestro) = await PlanDeSede.CrearAsync(
            sedes, sedeExplicita, sinFranjaSede: turnosConFranjaSinSede.Count > 0,
            new MotivosDeAviso(Mensajes.AvisoSinSede, Mensajes.AvisoSedeInactiva, Mensajes.AvisoSedeNoExiste), ct);
        if (falloMaestro is not null)
            return string.Format(Mensajes.RechazoDelDominio, falloMaestro);

        var resolucionCandidatos = await resolutorCandidatos.ResolverAsync(
            fechaDesde, selectorDeGrupo!.CodigoSede, selectorDeGrupo.Filtros, ct);
        if (resolucionCandidatos.FalloDeLectura is { } falloFichas)
            return string.Format(Mensajes.RechazoDelDominio, falloFichas);
        var candidatos = resolucionCandidatos.Candidatos;

        var asignacion = molde.AsignacionDesde(fechaDeInicio, turnosConFranjaSinSede);

        var contadores = new ContadoresDeEjecucion();
        ResultadoEjecucion ejecucion;
        try
        {
            ejecucion = await EjecutorDeProgramacion.EjecutarAsync(
                programacion, candidatos, asignacion, planDeSede!, ventana, contadores, ct);
        }
        finally
        {
            IndicadorDeEjecucion.Emitir(
                registro, relojDeEjecucion, inicio,
                new DatosDeIndicador(
                    "plantilla-turnos-grupo", candidatos.Count, molde.Nombre, sedeExplicita?.Id,
                    desdeEfectivo, fechaHasta, selectorDeGrupo.CodigoSede, selectorDeGrupo.EtiquetasComoTexto),
                contadores);
        }

        return RespuestaJson.Serializar(new PlantillaDeTurnosAplicadaPorGrupoResumen(
            Mensajes.ResultadoPlantillaAplicada,
            molde.Nombre,
            sedeExplicita is null ? null : new SedeResumen(sedeExplicita.Id, sedeExplicita.Nombre),
            ventana.ToString(),
            fechaDeInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            diasAntesDelInicio > 0 ? diasAntesDelInicio : null,
            selectorDeGrupo.Descripcion,
            candidatos.Count,
            ejecucion.Programados,
            ejecucion.Omitidos,
            ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual,
            ejecucion.Avisos));
    }
}

public sealed record PlantillaDeTurnosAplicadaPorGrupoResumen(
    string Resultado,
    string Plantilla,
    SedeResumen? Sede,
    string Ventana,
    string FechaInicio,
    int? DiasAntesDelInicio,
    string Selector,
    int GrupoResuelto,
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    string Nota,
    IReadOnlyList<AvisoDeSede>? Avisos = null);
