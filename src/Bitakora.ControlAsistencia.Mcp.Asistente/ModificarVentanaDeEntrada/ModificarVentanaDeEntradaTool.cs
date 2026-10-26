using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ModificarVentanaDeEntrada;

public partial class ModificarVentanaDeEntradaTool(ProgramacionApi programacion, ResolutorTurnoPorId resolutor)
{
    internal const string NombreTool = "modificar_ventana_de_entrada";

    [Function("ModificarVentanaDeEntrada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Modifica la ventana de entrada de un turno del catalogo, indicado por su id: el rango, "
            + "desde el inicio de su primera franja, dentro del cual la entrada del colaborador corre todo el turno. "
            + "amplitud en formato H:mm; 0:00 = turno fijo. Una ventana mayor que cero exige un turno con franjas "
            + "y que el inicio de su ultima franja mas la amplitud quede antes de las 24:00. "
            + "No cambia lo ya programado.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno_id", "Id del turno, tal como lo da buscar_turnos.", isRequired: true)]
        string turnoId,
        [McpToolProperty("amplitud", "Amplitud de la ventana en formato H:mm, por ejemplo 3:00.", isRequired: true)]
        string amplitud,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(turnoId))
            return string.Format(Mensajes.CampoObligatorio, "turno_id");
        if (!ValidadorDuracion.TryParsear(amplitud, out var duracion))
            return string.Format(Mensajes.AmplitudInvalida, amplitud);

        var resolucion = await resolutor.ResolverAsync(Guid.Empty, ct);
        if (resolucion.Ficha is not { } ficha)
            throw new NotImplementedException();

        var respuesta = await programacion.ModificarVentanaDeEntrada(ficha.Id, duracion, ct);
        if (await respuesta.LeerFalloAsync(ct) is { } fallo)
            return string.Format(Mensajes.RechazoDelDominio, fallo);

        return RespuestaJson.Serializar(new VentanaDeEntradaModificadaResumen(
            Mensajes.ResultadoVentanaModificada,
            ficha.Nombre,
            Describir(ficha, duracion),
            Mensajes.NotaVisibilidadEventual));
    }

    private static string Describir(FichaTurno ficha, HorasYMinutosJornada amplitud)
    {
        if (amplitud is { Horas: 0, Minutos: 0 })
            return Mensajes.TurnoFijo;

        var texto = TextoDeAmplitud(amplitud);
        if (ficha.Franjas.Count == 0)
            return string.Format(Mensajes.VentanaDeAmplitud, texto);

        var apertura = ficha.Franjas.Min(f => f.HoraInicio);
        var cierre = apertura.Add(new TimeSpan(amplitud.Horas, amplitud.Minutos, 0));
        return string.Format(Mensajes.EntraEntre, $"{apertura:HH\\:mm}", $"{cierre:HH\\:mm}", texto);
    }

    // Mismo formato que HorasYMinutos.ToString() del dominio, replicado como texto (MEF-ADR-0047 decision 3).
    private static string TextoDeAmplitud(HorasYMinutosJornada amplitud) => amplitud switch
    {
        { Minutos: 0 } => $"{amplitud.Horas} {Mensajes.Horas}",
        { Horas: 0 } => $"{amplitud.Minutos} {Mensajes.Minutos}",
        _ => $"{amplitud.Horas} {Mensajes.Horas} {amplitud.Minutos} {Mensajes.Minutos}",
    };
}

/// <summary>Eco de modificar_ventana_de_entrada: el 204 no trae body, se reconstruye con la amplitud enviada y las franjas de la ficha leida.</summary>
public sealed record VentanaDeEntradaModificadaResumen(string Resultado, string Turno, string VentanaDeEntrada, string Nota);
