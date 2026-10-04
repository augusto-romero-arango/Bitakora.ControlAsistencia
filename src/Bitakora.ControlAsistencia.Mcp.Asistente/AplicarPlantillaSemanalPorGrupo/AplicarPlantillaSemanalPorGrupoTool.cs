using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanalPorGrupo;

public partial class AplicarPlantillaSemanalPorGrupoTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores,
    ILogger<AplicarPlantillaSemanalPorGrupoTool>? logger = null, TimeProvider? reloj = null)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private readonly ILogger registro = (ILogger?)logger ?? NullLogger.Instance;
    private readonly TimeProvider relojDeEjecucion = reloj ?? TimeProvider.System;
    private readonly ResolutorPlantillaPorNombre resolutorPlantillas = new(programacion);
    private readonly ResolutorSedePorCodigo resolutorSedes = new(sedes);
    private readonly ResolutorCandidatosPorGrupo resolutorCandidatos = new(colaboradores);

    internal const string NombreTool = "aplicar_plantilla_semanal_por_grupo";

    [Function("AplicarPlantillaSemanalPorGrupo")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Aplica una plantilla semanal de turnos a todos los colaboradores de un grupo, definido por sede "
            + "y/o etiquetas (ambos criterios se combinan), durante una ventana de trabajo de maximo 35 dias. "
            + "La sede de programacion es opcional: sugierela al usuario y, si prefiere la sede de cada "
            + "colaborador, omite el parametro; no es la sede de trabajo ni el selector sede del grupo. Para "
            + "personas concretas usa aplicar_plantilla_semanal.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("plantilla", "Nombre de la plantilla semanal; miralo con listar_plantillas_semanales.", isRequired: true)]
        string plantilla,
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
        if (string.IsNullOrWhiteSpace(plantilla))
            return string.Format(Mensajes.CampoObligatorio, "plantilla");

        if (string.IsNullOrWhiteSpace(sede) && string.IsNullOrWhiteSpace(etiquetas))
            return Mensajes.SelectorObligatorio;

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

        var resolucion = await resolutorPlantillas.ResolverAsync(plantilla, ct);
        if (resolucion.FalloDeLectura is { } falloPlantillas)
            return string.Format(Mensajes.RechazoDelDominio, falloPlantillas);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.PlantillaNoExiste, plantilla, string.Join(", ", resolucion.NombresDisponibles));
        var cuadro = resolucion.Ficha;

        if (!cuadro.Completa)
            return string.Format(Mensajes.PlantillaIncompleta, cuadro.Nombre);
        if (cuadro.Dias.Any(d => d.Turno.Retirado || !d.Turno.Completo))
            return string.Format(Mensajes.PlantillaConTurnoNoProgramable, cuadro.Nombre);

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

        var (selectorDeGrupo, rechazoSelector) = await SelectorDeGrupo.ResolverAsync(
            sede, etiquetas, resolutorSedes,
            new MensajesDeSelector(
                Mensajes.SelectorObligatorio, Mensajes.EtiquetaMalFormada, Mensajes.SedeDelSelectorNoExiste,
                Mensajes.SedeDelSelectorInactiva, Mensajes.RechazoDelDominio), ct);
        if (rechazoSelector is not null)
            return rechazoSelector;

        var idsDelMolde = cuadro.Dias.Select(d => Guid.Parse(d.Turno.Id)).ToHashSet();
        HashSet<Guid> turnosConFranjaSinSede = [];
        if (sedeExplicita is null)
        {
            var respuestaTurnos = await programacion.ListarTurnos(ct);
            if (await respuestaTurnos.LeerFalloAsync(ct) is { } falloTurnos)
                return string.Format(Mensajes.RechazoDelDominio, falloTurnos);

            var catalogo = await respuestaTurnos.Content.ReadFromJsonAsync<List<FichaTurno>>(OpcionesLectura, ct) ?? [];
            turnosConFranjaSinSede = catalogo
                .Select(TurnoAProgramar.De)
                .Where(t => t.TieneFranjaSinSede && idsDelMolde.Contains(t.Id))
                .Select(t => t.Id)
                .ToHashSet();
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

        var asignacionDeFechas = AsignacionDePlantilla.Crear(cuadro, fechaDesde);
        var asignacion = AsignacionDeTurno.PorFecha(fecha =>
        {
            var turno = asignacionDeFechas.Para(fecha);
            var id = Guid.Parse(turno.Id);
            return new TurnoAProgramar(id, turno.Nombre ?? string.Empty, turnosConFranjaSinSede.Contains(id));
        });

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
                    "plantilla-grupo", candidatos.Count, cuadro.Nombre, sedeExplicita?.Id,
                    fechaDesde, fechaHasta, selectorDeGrupo.CodigoSede, selectorDeGrupo.EtiquetasComoTexto),
                contadores);
        }

        return RespuestaJson.Serializar(new PlantillaAplicadaPorGrupoResumen(
            Mensajes.ResultadoPlantillaAplicada,
            cuadro.Nombre,
            sedeExplicita is null ? null : new SedeResumen(sedeExplicita.Id, sedeExplicita.Nombre),
            ventana.ToString(),
            selectorDeGrupo.Descripcion,
            candidatos.Count,
            ejecucion.Programados,
            ejecucion.Omitidos,
            ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual,
            ejecucion.Avisos));
    }
}

public sealed record PlantillaAplicadaPorGrupoResumen(
    string Resultado,
    string Plantilla,
    SedeResumen? Sede,
    string Ventana,
    string Selector,
    int GrupoResuelto,
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    string Nota,
    IReadOnlyList<AvisoDeSede>? Avisos = null);
