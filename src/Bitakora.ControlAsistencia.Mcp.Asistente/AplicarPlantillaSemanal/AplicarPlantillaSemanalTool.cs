using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;

public partial class AplicarPlantillaSemanalTool(
    ProgramacionApi programacion, SedesApi sedes, ColaboradoresApi colaboradores,
    ILogger<AplicarPlantillaSemanalTool>? logger = null, TimeProvider? reloj = null)
{
    private static readonly JsonSerializerOptions OpcionesLectura = new(JsonSerializerDefaults.Web);

    private readonly ILogger registro = (ILogger?)logger ?? NullLogger.Instance;
    private readonly TimeProvider relojDeEjecucion = reloj ?? TimeProvider.System;
    private readonly ResolutorPlantillaPorNombre resolutorPlantillas = new(programacion);
    private readonly ResolutorSedePorCodigo resolutorSedes = new(sedes);
    private readonly ResolutorCandidatosPorLista resolutorCandidatos = new(colaboradores);

    internal const string NombreTool = "aplicar_plantilla_semanal";

    [Function("AplicarPlantillaSemanal")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Aplica una plantilla semanal de turnos a una lista de colaboradores durante una ventana de "
            + "trabajo de maximo 35 dias. A cada dia le programa el turno que el molde asigna a esa semana "
            + "y dia; el molde se alinea a la semana (lunes a domingo) que contiene 'desde'. Recibe el "
            + "nombre de la plantilla (miralo con listar_plantillas_semanales), opcionalmente el codigo de "
            + "la sede de programacion -- distinta de la sede de trabajo de cada colaborador; sugierela al "
            + "usuario y, si prefiere la sede de cada colaborador, omite el parametro -- y las "
            + "identificaciones completas separadas por coma, tal como las devuelven buscar_colaboradores "
            + "o listar_colaboradores. A cada colaborador le programa solo los dias que su vinculacion "
            + "cubre y respeta sus ausencias.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("desde", "Primer dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string desde,
        [McpToolProperty("hasta", "Ultimo dia de la ventana de trabajo, formato yyyy-MM-dd.", isRequired: true)]
        string hasta,
        [McpToolProperty("plantilla", "Nombre de la plantilla semanal; miralo con listar_plantillas_semanales.", isRequired: true)]
        string plantilla,
        [McpToolProperty("sede_de_programacion", "Opcional. Codigo de la sede donde se registra la programacion; sugierela al usuario. Si prefiere la de cada colaborador, omitela.", isRequired: false)]
        string? sedeDeProgramacion,
        [McpToolProperty("identificaciones", "Identificaciones completas separadas por coma ('CC-79879078, CE-887766').", isRequired: true)]
        string identificaciones,
        CancellationToken ct)
    {
        var inicio = relojDeEjecucion.GetTimestamp();
        if (string.IsNullOrWhiteSpace(desde))
            return string.Format(Mensajes.CampoObligatorio, "desde");
        if (string.IsNullOrWhiteSpace(hasta))
            return string.Format(Mensajes.CampoObligatorio, "hasta");
        if (string.IsNullOrWhiteSpace(plantilla))
            return string.Format(Mensajes.CampoObligatorio, "plantilla");
        if (string.IsNullOrWhiteSpace(identificaciones))
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

        var solicitadas = identificaciones
            .Split(',')
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToList();
        if (solicitadas.Count == 0)
            return string.Format(Mensajes.CampoObligatorio, "identificaciones");

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

        // Una sola lectura del catalogo de turnos, solo cuando la cascada la necesita (CA-ADR-0038).
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

        var resolucionCandidatos = await resolutorCandidatos.ResolverAsync(solicitadas, ct);
        if (resolucionCandidatos.FalloDeLectura is { } falloDirectorio)
            return string.Format(Mensajes.RechazoDelDominio, falloDirectorio);
        var candidatos = resolucionCandidatos.Candidatos;

        var asignacionDeFechas = AsignacionDePlantilla.Crear(cuadro, fechaDesde);
        var asignacion = AsignacionDeTurno.PorFecha(fecha =>
        {
            var turno = asignacionDeFechas.Para(fecha);
            var id = Guid.Parse(turno.Id);
            return new TurnoAProgramar(id, turno.Nombre ?? string.Empty, turnosConFranjaSinSede.Contains(id));
        });

        var omitidosPorDirectorio = solicitadas.Count - candidatos.Count;
        var contadores = new ContadoresDeEjecucion { Omitidos = omitidosPorDirectorio };
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
                    "plantilla", candidatos.Count, cuadro.Nombre, sedeExplicita?.Id,
                    fechaDesde, fechaHasta, null, null),
                contadores);
        }

        return RespuestaJson.Serializar(new PlantillaAplicadaResumen(
            Mensajes.ResultadoPlantillaAplicada,
            cuadro.Nombre,
            sedeExplicita is null ? null : new SedeResumen(sedeExplicita.Id, sedeExplicita.Nombre),
            ventana.ToString(),
            ejecucion.Programados,
            ejecucion.Omitidos + omitidosPorDirectorio,
            ejecucion.Fallidos,
            Mensajes.NotaVisibilidadEventual,
            ejecucion.Avisos));
    }
}

public sealed record PlantillaAplicadaResumen(
    string Resultado,
    string Plantilla,
    SedeResumen? Sede,
    string Ventana,
    IReadOnlyList<ColaboradorProgramadoResumen> Programados,
    int Omitidos,
    IReadOnlyList<ColaboradorFallidoResumen>? Fallidos,
    string Nota,
    IReadOnlyList<AvisoDeSede>? Avisos = null);
