using System.Net.Http.Json;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.BuscarTurnos;

public partial class BuscarTurnosTool(ProgramacionApi api)
{
    internal const string NombreTool = "buscar_turnos";
    internal const int MaximoTurnos = 20;

    [Function("BuscarTurnos")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Busca turnos del catalogo para saber a cual se refiere el usuario cuando lo nombra de "
            + "forma aproximada. Por nombre: palabras en cualquier orden, sin importar acentos ni "
            + "mayusculas, tambien incompletas ('vigilancia noct') o con una letra de diferencia. "
            + "Por horario: una hora ('06:00', el que entra a las 6) o un rango ('06:00-14:00', el "
            + "de 6 a 2; '22:00-06:00' si cruza la medianoche); primero salen los de horario "
            + "exacto, luego los mas cercanos y al final los que solo se cruzan. Por sede: codigo "
            + "o nombre de la sede de alguna franja. Combina los criterios. Devuelve hasta 20 "
            + "candidatos con el motivo de la coincidencia; si hay mas de uno plausible, pregunta "
            + "al usuario antes de actuar. Para ver todos los que cumplen un criterio, usa "
            + "listar_turnos: esta busqueda devuelve hasta 20 candidatos para ubicar uno; para el "
            + "detalle, obtener_turno.")]
        [McpMetadata("""{"readOnlyHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre o parte del nombre del turno, en cualquier orden.")]
        string? nombre,
        [McpToolProperty("horario", "Hora (HH:mm) o rango (HH:mm-HH:mm), ej. '06:00-14:00'.")]
        string? horario,
        [McpToolProperty("sede", "Codigo o nombre de la sede de alguna franja.")]
        string? sede,
        CancellationToken ct)
    {
        var nombrePedido = Limpiar(nombre);
        var horarioPedido = Limpiar(horario);
        var sedePedida = Limpiar(sede);

        if (nombrePedido is null && horarioPedido is null && sedePedida is null)
            return Mensajes.FaltaCriterio;

        PuntajeDeHorario? puntaje = null;
        if (horarioPedido is not null && !PuntajeDeHorario.TryParsear(horarioPedido, out puntaje))
            return string.Format(Mensajes.HorarioMalFormado, horarioPedido);

        var respuesta = await api.ListarTurnos(ct);
        respuesta.EnsureSuccessStatusCode();

        var catalogo = (await respuesta.Content.ReadFromJsonAsync<SobreDeLista<FichaTurno>>(ct))?.Elementos ?? [];

        var candidatos = catalogo
            .Select(turno => Evaluar(turno, nombrePedido, puntaje, sedePedida))
            .OfType<Candidato>()
            .OrderBy(c => c.DistanciaHorario)
            .ThenByDescending(c => c.PalabrasCoincidentes)
            .ThenBy(c => c.CalidadNombre)
            .ThenBy(c => c.Turno.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidatos.Count == 0)
            return string.Format(
                Mensajes.SinCoincidencias, DescribirCriterios(nombrePedido, horarioPedido, sedePedida));

        var visibles = candidatos.Take(MaximoTurnos).Select(c => c.Remodelar()).ToList();
        var nota = candidatos.Count > visibles.Count ? Mensajes.NotaTruncado : null;

        return RespuestaJson.Serializar(new CoincidenciasDeTurnos(visibles.Count, visibles, nota));
    }

    private static Candidato? Evaluar(
        FichaTurno turno, string? nombre, PuntajeDeHorario? puntaje, string? sede)
    {
        var motivos = new List<string>();
        var distancia = 0;
        var palabras = 0;
        var calidad = 0;

        if (nombre is not null)
        {
            var coincidencias = CoincidenciaDeTexto.Evaluar(nombre, turno.Nombre);
            if (coincidencias.Count == 0)
                return null;

            palabras = coincidencias.Count;
            calidad = coincidencias.Sum(c => (int)c.Tipo);
            motivos.Add(string.Format(Mensajes.CoincidePorNombre, string.Join(", ", coincidencias.Select(Describir))));
        }

        if (puntaje is not null)
        {
            var horario = puntaje.Evaluar(turno.Franjas);
            if (horario is null)
                return null;

            distancia = horario.DistanciaEnMinutos;
            motivos.Add(DescribirHorario(horario));
        }

        if (sede is not null)
        {
            var franja = turno.Franjas.FirstOrDefault(f => CoincideSede(f, sede));
            if (franja is null)
                return null;

            motivos.Add(string.Format(Mensajes.CoincidePorSede, franja.NombreSede ?? franja.SedeId));
        }

        return new Candidato(turno, motivos, distancia, palabras, calidad);
    }

    private static bool CoincideSede(FranjaFicha franja, string sede) =>
        string.Equals(franja.SedeId, sede, StringComparison.OrdinalIgnoreCase)
        || (franja.NombreSede is not null && CoincidenciaDeTexto.Evaluar(sede, franja.NombreSede).Count > 0);

    private static string Describir(PalabraCoincidente c) =>
        c.Tipo == TipoCoincidenciaDePalabra.Exacta ? c.Pedida : string.Format(Mensajes.PalabraAproximada, c.Pedida, c.Encontrada);

    private static string DescribirHorario(CoincidenciaDeHorario h)
    {
        var plantilla = h.Tipo switch
        {
            TipoCoincidenciaDeHorario.Exacto => Mensajes.CoincidePorHorarioExacto,
            TipoCoincidenciaDeHorario.Cercano => Mensajes.CoincidePorHorarioCercano,
            _ => Mensajes.CoincidePorHorarioSeCruza
        };

        return string.Format(plantilla, Rango(h.Franja));
    }

    private static string Rango(FranjaFicha f) =>
        NotacionFranja.Rango(f.HoraInicio, f.HoraFin, diaOffsetInicio: 0, f.DiaOffsetFin);

    private static string DescribirCriterios(string? nombre, string? horario, string? sede) =>
        string.Join(", ", new[]
        {
            nombre is null ? null : string.Format(Mensajes.CriterioNombre, nombre),
            horario is null ? null : string.Format(Mensajes.CriterioHorario, horario),
            sede is null ? null : string.Format(Mensajes.CriterioSede, sede)
        }.OfType<string>());

    private static string? Limpiar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private sealed record Candidato(
        FichaTurno Turno, List<string> Motivos, int DistanciaHorario, int PalabrasCoincidentes, int CalidadNombre)
    {
        public TurnoEncontrado Remodelar() => new(
            Turno.Id,
            Turno.Nombre,
            string.Join(", ", Turno.Franjas.Select(Rango)),
            Motivos,
            Turno.EsDescanso ? true : null,
            Turno.Completo || Turno.EsDescanso ? null : true);
    }
}

public sealed record CoincidenciasDeTurnos(
    int Mostrando,
    IReadOnlyList<TurnoEncontrado> Turnos,
    string? Nota);

public sealed record TurnoEncontrado(
    string Id,
    string Nombre,
    string Horario,
    IReadOnlyList<string> CoincidePor,
    bool? EsDescanso,
    bool? EnConstruccion,
    bool? ConVentanaDeEntrada = null);
