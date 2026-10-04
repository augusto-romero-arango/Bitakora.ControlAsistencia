using System.Globalization;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurnoPorGrupo;

public partial class SolicitarProgramacionTurnoPorGrupoTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores,
    ILogger<SolicitarProgramacionTurnoPorGrupoTool>? logger = null, TimeProvider? reloj = null)
{
    private readonly ILogger registro = (ILogger?)logger ?? NullLogger.Instance;
    private readonly TimeProvider relojDeEjecucion = reloj ?? TimeProvider.System;

    internal const string NombreTool = "solicitar_programacion_turno_por_grupo";

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);
    private readonly ResolutorSedePorCodigo resolutorSedes = new(sedes);
    private readonly ResolutorCandidatosPorGrupo resolutorCandidatos = new(colaboradores);

    [Function("SolicitarProgramacionTurnoPorGrupo")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Programa un turno a todos los colaboradores de un grupo, definido por sede y/o etiquetas "
            + "(ambos criterios se combinan), para los dias de una ventana de maximo 35 dias. La sede de "
            + "programacion es opcional: sugierela al usuario y, si prefiere la sede de cada colaborador, "
            + "omite el parametro; no es la sede de trabajo ni el selector sede del grupo. Para personas concretas usa solicitar_programacion_turno; los "
            + "fallidos se reintentan con esa tool.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("turno", "Nombre exacto del turno del catalogo.", isRequired: true)]
        string turno,
        [McpToolProperty(
            "sede_de_programacion",
            "Opcional. Codigo de la sede donde se registra la programacion; sugierela al usuario. "
            + "Si prefiere la de cada colaborador, omitela. No es la sede de trabajo del grupo.",
            isRequired: false)]
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
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");

        var codigoSedeSelector = string.IsNullOrWhiteSpace(sede) ? null : sede.Trim();
        var pares = (etiquetas ?? string.Empty)
            .Split(',')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
        if (codigoSedeSelector is null && pares.Count == 0)
            return Mensajes.SelectorObligatorio;

        var filtros = new List<FiltroEtiqueta>();
        foreach (var par in pares)
        {
            var partes = par.Split(':', 2);
            if (partes.Length < 2 || partes[0].Trim().Length == 0 || partes[1].Trim().Length == 0)
                return string.Format(Mensajes.EtiquetaMalFormada, par);
            filtros.Add(new FiltroEtiqueta(partes[0].Trim(), partes[1].Trim()));
        }

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

        string? codigoCanonicoSelector = null;
        if (codigoSedeSelector is not null)
        {
            var resolucionSelector = await resolutorSedes.ResolverAsync(codigoSedeSelector, ct);
            if (resolucionSelector.FalloDeLectura is { } falloSelector)
                return string.Format(Mensajes.RechazoDelDominio, falloSelector);
            if (resolucionSelector.MensajeDelMotivo(
                codigoSedeSelector,
                noExiste: Mensajes.SedeDelSelectorNoExiste,
                inactiva: Mensajes.SedeDelSelectorInactiva) is { } rechazoSelector)
                return rechazoSelector;
            codigoCanonicoSelector = resolucionSelector.Sede!.Id;
        }

        var (planDeSede, falloMaestro) = await PlanDeSede.CrearAsync(
            sedes, sedeExplicita, fichaTurno,
            new MotivosDeAviso(Mensajes.AvisoSinSede, Mensajes.AvisoSedeInactiva, Mensajes.AvisoSedeNoExiste), ct);
        if (falloMaestro is not null)
            return string.Format(Mensajes.RechazoDelDominio, falloMaestro);

        var resolucionCandidatos = await resolutorCandidatos.ResolverAsync(
            fechaDesde, codigoCanonicoSelector, filtros, ct);
        if (resolucionCandidatos.FalloDeLectura is { } falloFichas)
            return string.Format(Mensajes.RechazoDelDominio, falloFichas);
        var candidatos = resolucionCandidatos.Candidatos;

        var contadores = new ContadoresDeEjecucion();
        ResultadoEjecucion ejecucion;
        try
        {
            ejecucion = await EjecutorDeProgramacion.EjecutarAsync(
                programacion, candidatos, Guid.Parse(fichaTurno.Id), planDeSede!, ventana, contadores, ct);
        }
        finally
        {
            IndicadorDeEjecucion.Emitir(
                registro, relojDeEjecucion, inicio,
                new DatosDeIndicador(
                    "grupo", candidatos.Count, fichaTurno.Nombre, sedeExplicita?.Id,
                    fechaDesde, fechaHasta, codigoCanonicoSelector,
                    filtros.Count == 0 ? null : string.Join(", ", filtros.Select(f => $"{f.Categoria}:{f.Valor}"))),
                contadores);
        }

        var selector = string.Join(
            ", ",
            (codigoCanonicoSelector is null ? [] : new[] { $"sede:{codigoCanonicoSelector}" })
                .Concat(filtros.Select(f => $"{f.Categoria}:{f.Valor}")));

        return RespuestaJson.Serializar(new ProgramacionPorGrupoResumen(
            Mensajes.ResultadoProgramacionSolicitada,
            fichaTurno.Nombre,
            sedeExplicita is null ? null : new SedeResumen(sedeExplicita.Id, sedeExplicita.Nombre),
            ventana.ToString(),
            selector,
            candidatos.Count,
            ejecucion.Programados,
            ejecucion.Omitidos,
            ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual,
            ejecucion.Avisos));
    }
}

public sealed record ProgramacionPorGrupoResumen(
    string Resultado,
    string Turno,
    SedeResumen? Sede,
    string Ventana,
    string Selector,
    int GrupoResuelto,
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    string Nota,
    IReadOnlyList<AvisoDeSede>? Avisos = null);
