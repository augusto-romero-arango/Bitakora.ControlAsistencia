using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurnoDeHorarioAbierto;

public partial class CrearTurnoDeHorarioAbiertoTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "crear_turno_de_horario_abierto";

    [Function("CrearTurnoDeHorarioAbierto")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Crea un turno de horario abierto: el usuario solo dice cuantas horas se trabajan y el colaborador "
            + "entra a la hora que quiera del dia. Queda una franja que empieza a las 04:00 y dura `duracion`, "
            + "con una ventana de entrada hasta las 24:00 menos la duracion, de modo que la salida mas tardia "
            + "son las 24:00. Nace completo y programable, y se sigue disenando como cualquier turno. El nombre "
            + "es unico en el catalogo; si ya existe, responde el rechazo del dominio. Aparece en listar_turnos "
            + "en unos segundos.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty("nombre", "Nombre unico del turno en el catalogo.", isRequired: true)]
        string nombre,
        [McpToolProperty(
            "duracion",
            "Horas que se trabajan en formato H:mm, por ejemplo 6:00; mayor que 0:00 y hasta 20:00.",
            isRequired: true)]
        string duracion,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return string.Format(Mensajes.CampoObligatorio, "nombre");
        if (!ValidadorDuracion.TryParsear(duracion, out var horasYMinutos))
            return string.Format(Mensajes.DuracionInvalida, duracion);

        var turnoId = Guid.CreateVersion7();
        var respuesta = await programacion.CrearTurnoDeHorarioAbierto(turnoId, nombre, horasYMinutos, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        var tiempo = new TimeSpan(horasYMinutos.Horas, horasYMinutos.Minutos, 0);
        return RespuestaJson.Serializar(new TurnoDeHorarioAbiertoCreadoResumen(
            Mensajes.ResultadoTurnoCreado,
            new TurnoDeHorarioAbiertoCreadoEco(
                turnoId.ToString(),
                nombre,
                $"{horasYMinutos.Horas}:{horasYMinutos.Minutos:00}",
                $"{Hora(Apertura)}-{Hora(Apertura + tiempo)}",
                DescribirEntrada(tiempo)),
            Mensajes.NotaVisibilidadEventual));
    }

    // Replica la regla del dominio solo para el eco (MEF-ADR-0047 decision 3): si el dominio cambia la
    // apertura o la salida mas tardia, cambian aqui y en la descripcion de la tool.
    private static readonly TimeSpan Apertura = TimeSpan.FromHours(4);
    private static readonly TimeSpan CierreMasTardio = TimeSpan.FromHours(24);

    private static string DescribirEntrada(TimeSpan duracion)
    {
        var entradaMasTardia = CierreMasTardio - duracion;
        return entradaMasTardia == Apertura
            ? string.Format(Mensajes.TurnoFijo, Hora(Apertura))
            : string.Format(Mensajes.EntraEntre, Hora(Apertura), Hora(entradaMasTardia));
    }

    private static string Hora(TimeSpan momento) =>
        TimeSpan.FromTicks(momento.Ticks % TimeSpan.TicksPerDay).ToString(@"hh\:mm");
}

/// <summary>Eco de crear_turno_de_horario_abierto: el 201 no trae body, se reconstruye con lo enviado y la regla de apertura.</summary>
public sealed record TurnoDeHorarioAbiertoCreadoResumen(string Resultado, TurnoDeHorarioAbiertoCreadoEco Turno, string Nota);

public sealed record TurnoDeHorarioAbiertoCreadoEco(string Id, string Nombre, string Duracion, string Franja, string Entrada);
