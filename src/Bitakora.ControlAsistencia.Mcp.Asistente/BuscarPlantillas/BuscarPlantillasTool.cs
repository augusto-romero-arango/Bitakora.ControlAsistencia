using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillas;

public partial class BuscarPlantillasTool(ProgramacionApi api)
{
    internal const string NombreTool = "buscar_plantillas";
    internal const int MaximoPlantillas = 20;
    private const int MinimoDias = 1;
    private const int MaximoDias = 42;
    private const string EstadoIncompleta = "incompleta";
    private const string EstadoConAdvertencias = "con_advertencias";

    [Function("BuscarPlantillas")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Busca plantillas de turnos del catalogo para saber a cual se refiere el usuario cuando la "
            + "nombra de forma aproximada. Por nombre: palabras en cualquier orden, sin importar acentos "
            + "ni mayusculas, tambien incompletas o con una letra de diferencia. Por turno: plantillas "
            + "que usan en algun dia un turno con ese nombre aproximado. Por dias del ciclo: numero "
            + "exacto (1 a 42). Por estado: incompleta (algun dia sin turno) o con_advertencias (no "
            + "cumple su Jornada o no tiene Jornada). Combina los criterios. Devuelve hasta 20 "
            + "candidatas con el motivo de la coincidencia; si hay mas de una plausible, pregunta al "
            + "usuario antes de actuar. Para ver todo el catalogo usa listar_plantillas; para el "
            + "cuadro, obtener_plantilla.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre o parte del nombre de la plantilla, en cualquier orden.")]
        string? nombre,
        [McpToolProperty("turno", "Nombre o parte del nombre de un turno que la plantilla usa en algun dia.")]
        string? turno,
        [McpToolProperty("dias_del_ciclo", "Numero de dias del ciclo de la plantilla, de 1 a 42.")]
        string? diasDelCiclo,
        [McpToolProperty("estado", "incompleta o con_advertencias.")]
        string? estado,
        CancellationToken ct)
    {
        var nombrePedido = Limpiar(nombre);
        var turnoPedido = Limpiar(turno);
        var diasPedidos = Limpiar(diasDelCiclo);
        var estadoPedido = Limpiar(estado);

        if (nombrePedido is null && turnoPedido is null && diasPedidos is null && estadoPedido is null)
            return Mensajes.FaltaCriterio;

        int? numeroDias = null;
        if (diasPedidos is not null)
        {
            if (!int.TryParse(diasPedidos, out var valor) || valor is < MinimoDias or > MaximoDias)
                return string.Format(Mensajes.DiasInvalidos, diasPedidos);

            numeroDias = valor;
        }

        var estadoNormalizado = estadoPedido?.ToLowerInvariant();
        if (estadoNormalizado is not (null or EstadoIncompleta or EstadoConAdvertencias))
            return string.Format(Mensajes.EstadoDesconocido, estadoPedido);

        var respuesta = await api.ListarPlantillasTurnos(ct);
        respuesta.EnsureSuccessStatusCode();

        var catalogo = (await respuesta.Content.ReadFromJsonAsync<SobreDeLista<CuadroDeTurnos>>(ct))?.Elementos ?? [];

        var candidatas = catalogo
            .Select(p => Evaluar(p, nombrePedido, turnoPedido, numeroDias, estadoNormalizado))
            .OfType<Candidata>()
            .OrderByDescending(c => c.PalabrasNombre)
            .ThenBy(c => c.CalidadNombre)
            .ThenByDescending(c => c.PalabrasTurno)
            .ThenBy(c => c.CalidadTurno)
            .ThenBy(c => c.Plantilla.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidatas.Count == 0)
            return string.Format(
                Mensajes.SinCoincidencias,
                DescribirCriterios(nombrePedido, turnoPedido, diasPedidos, estadoNormalizado));

        var visibles = candidatas.Take(MaximoPlantillas).Select(c => c.Remodelar()).ToList();
        var nota = candidatas.Count > visibles.Count ? Mensajes.NotaTruncado : null;

        return RespuestaJson.Serializar(new CoincidenciasDePlantillas(visibles.Count, visibles, nota));
    }

    private static Candidata? Evaluar(
        CuadroDeTurnos plantilla, string? nombre, string? turno, int? dias, string? estado)
    {
        var motivos = new List<string>();
        var palabrasNombre = 0;
        var calidadNombre = 0;
        var palabrasTurno = 0;
        var calidadTurno = 0;

        if (nombre is not null)
        {
            var coincidencias = CoincidenciaDeTexto.Evaluar(nombre, plantilla.Nombre);
            if (coincidencias.Count == 0)
                return null;

            palabrasNombre = coincidencias.Count;
            calidadNombre = coincidencias.Sum(c => (int)c.Tipo);
            motivos.Add(string.Format(Mensajes.CoincidePorNombre, string.Join(", ", coincidencias.Select(Describir))));
        }

        if (turno is not null)
        {
            var aparicion = plantilla.Dias
                .Where(d => d.Turno.Nombre is not null)
                .OrderBy(d => d.Dia)
                .Select(d => (Dia: d, Coincidencias: CoincidenciaDeTexto.Evaluar(turno, d.Turno.Nombre!)))
                .FirstOrDefault(x => x.Coincidencias.Count > 0);
            if (aparicion.Dia is null)
                return null;

            palabrasTurno = aparicion.Coincidencias.Count;
            calidadTurno = aparicion.Coincidencias.Sum(c => (int)c.Tipo);
            motivos.Add(string.Format(Mensajes.CoincidePorTurno, aparicion.Dia.Turno.Nombre, aparicion.Dia.Dia));
        }

        if (dias is not null)
        {
            if (plantilla.DiasDelCiclo != dias)
                return null;

            motivos.Add(string.Format(Mensajes.CoincidePorDias, plantilla.DiasDelCiclo));
        }

        if (estado == EstadoIncompleta)
        {
            if (plantilla.Completa)
                return null;

            motivos.Add(Mensajes.CoincidePorIncompleta);
        }
        else if (estado == EstadoConAdvertencias)
        {
            if (NumeroDeAdvertencias(plantilla) == 0)
                return null;

            motivos.Add(Mensajes.CoincidePorConAdvertencias);
        }

        return new Candidata(plantilla, motivos, palabrasNombre, calidadNombre, palabrasTurno, calidadTurno);
    }

    private static int NumeroDeAdvertencias(CuadroDeTurnos plantilla) => plantilla.Advertencias?.Count ?? 0;

    private static string Describir(PalabraCoincidente c) =>
        c.Tipo == TipoCoincidenciaDePalabra.Exacta ? c.Pedida : string.Format(Mensajes.PalabraAproximada, c.Pedida, c.Encontrada);

    private static string DescribirCriterios(string? nombre, string? turno, string? dias, string? estado) =>
        string.Join(", ", new[]
        {
            nombre is null ? null : string.Format(Mensajes.CriterioNombre, nombre),
            turno is null ? null : string.Format(Mensajes.CriterioTurno, turno),
            dias is null ? null : string.Format(Mensajes.CriterioDias, dias),
            estado is null ? null : string.Format(Mensajes.CriterioEstado, estado)
        }.OfType<string>());

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private sealed record Candidata(
        CuadroDeTurnos Plantilla,
        List<string> Motivos,
        int PalabrasNombre,
        int CalidadNombre,
        int PalabrasTurno,
        int CalidadTurno)
    {
        public PlantillaEncontrada Remodelar()
        {
            var advertencias = NumeroDeAdvertencias(Plantilla);
            return new PlantillaEncontrada(
                Plantilla.Id,
                Plantilla.Nombre,
                Plantilla.DiasDelCiclo,
                Motivos,
                Plantilla.Completa ? null : true,
                advertencias > 0 ? advertencias : null);
        }
    }
}

public sealed record CoincidenciasDePlantillas(
    int Mostrando,
    IReadOnlyList<PlantillaEncontrada> Plantillas,
    string? Nota);

public sealed record PlantillaEncontrada(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    IReadOnlyList<string> CoincidePor,
    bool? Incompleta,
    int? Advertencias);
