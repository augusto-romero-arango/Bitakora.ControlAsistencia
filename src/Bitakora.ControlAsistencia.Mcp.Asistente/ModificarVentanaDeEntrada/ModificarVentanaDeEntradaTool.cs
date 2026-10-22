using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.ModificarVentanaDeEntrada;

public partial class ModificarVentanaDeEntradaTool(ProgramacionApi programacion, ResolutorTurnoPorNombre resolutor)
{
    private readonly ProgramacionApi _programacion = programacion;
    private readonly ResolutorTurnoPorNombre _resolutor = resolutor;

    internal const string NombreTool = "modificar_ventana_de_entrada";

    [Function("ModificarVentanaDeEntrada")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Modifica la ventana de entrada de un turno del catalogo, indicado por su nombre exacto: el rango, "
            + "desde el inicio de su primera franja, dentro del cual la entrada del colaborador corre todo el turno. "
            + "amplitud en formato H:mm; 0:00 = turno fijo. Una ventana mayor que cero exige un turno con franjas, "
            + "que la primera franja empiece desde las 04:00 y que la ultima franja mas la amplitud termine antes "
            + "de las 24:00. No cambia lo ya programado.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false, "idempotentHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno", "Nombre exacto del turno del catalogo (miralo con buscar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty("amplitud", "Amplitud de la ventana en formato H:mm, por ejemplo 3:00.", isRequired: true)]
        string amplitud,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");
        if (!ValidadorDuracion.TryParsear(amplitud, out var duracion))
            return string.Format(Mensajes.AmplitudInvalida, amplitud);

        var resolucion = await _resolutor.ResolverAsync(turno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.TurnoNoExiste, turno, string.Join(", ", resolucion.NombresDisponibles));
        var ficha = resolucion.Ficha;

        var respuesta = await _programacion.ModificarVentanaDeEntrada(ficha.Id, duracion, ct);
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
        if (amplitud.Horas == 0 && amplitud.Minutos == 0)
            return Mensajes.TurnoFijo;

        var texto = TextoDeAmplitud(amplitud);
        if (ficha.Franjas.Count == 0)
            return string.Format(Mensajes.VentanaDeAmplitud, texto);

        var apertura = ficha.Franjas.Min(f => f.HoraInicio);
        var cierre = apertura.Add(new TimeSpan(amplitud.Horas, amplitud.Minutos, 0));
        return string.Format(Mensajes.EntraEntre, $"{apertura:HH\\:mm}", $"{cierre:HH\\:mm}", texto);
    }

    private static string TextoDeAmplitud(HorasYMinutosJornada amplitud) =>
        string.Join(
            " ",
            new[]
            {
                amplitud.Horas > 0 ? $"{amplitud.Horas} h" : null,
                amplitud.Minutos > 0 ? $"{amplitud.Minutos} min" : null,
            }.OfType<string>());
}

/// <summary>Eco de modificar_ventana_de_entrada: el 204 no trae body, se reconstruye con lo enviado.</summary>
public sealed record VentanaDeEntradaModificadaResumen(string Resultado, string Turno, string VentanaDeEntrada, string Nota);
