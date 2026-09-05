using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.AsignarSedeFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos (resolver nombre -> id, via
// ResolutorTurnoPorNombre) + GET sedes/fichas/{codigo} (solo con codigo_sede) + POST
// programacion/turnos/{id}:asignar-sede-franja (paso 4 MEF-ADR-0043). Sin codigo_sede la tool
// retira la sede prearmada (body sin la clave sede); el 400/404/409 del POST -- incluido
// FranjaSinSede al retirar sin sede -- se traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class AsignarSedeFranjaTool(ProgramacionApi programacion, SedesApi sedes)
{
    internal const string NombreTool = "asignar_sede_franja";

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);
    private readonly ResolutorSedePorCodigo resolutorSedes = new(sedes);

    [Function("AsignarSedeFranja")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Asigna o cambia la sede prearmada de una franja ordinaria de un turno, indicando el "
            + "turno por su nombre exacto, la franja por su hora de inicio (HH:mm) y la sede por "
            + "su codigo (obtenlo con listar_sedes). Sin codigo_sede, retira la sede prearmada de "
            + "esa franja. No cambia el horario ni los descansos de la franja.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": false}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno", "Nombre exacto del turno del catalogo (miralo con listar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty("franja", "Hora de inicio de la franja, formato HH:mm.", isRequired: true)]
        string franja,
        [McpToolProperty(
            "codigo_sede",
            "Codigo de la sede a prearmar en esta franja (miralo con listar_sedes). Ausente o "
            + "vacio retira la sede prearmada de la franja.")]
        string? codigoSede,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");
        if (string.IsNullOrWhiteSpace(franja))
            return string.Format(Mensajes.CampoObligatorio, "franja");

        if (!NotacionFranja.TryParseHora(franja, out var horaFranja))
            return string.Format(Mensajes.HoraInvalida, "franja", franja);

        var resolucion = await resolutor.ResolverAsync(turno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.TurnoNoExiste, turno, string.Join(", ", resolucion.NombresDisponibles));
        var fichaTurno = resolucion.Ficha;

        SedeProgramada? sede = null;
        if (!string.IsNullOrWhiteSpace(codigoSede))
        {
            var resolucionSede = await resolutorSedes.ResolverAsync(codigoSede, ct);
            if (resolucionSede.FalloDeLectura is { } falloSede)
                return string.Format(Mensajes.RechazoDelDominio, falloSede);
            if (resolucionSede.MensajeDelMotivo(
                codigoSede, noExiste: Mensajes.SedeNoExiste, inactiva: Mensajes.SedeInactiva) is { } rechazo)
                return rechazo;

            sede = resolucionSede.Sede;
        }

        var respuestaAsignar = await programacion.AsignarSedeAFranja(
            fichaTurno.Id, new SedeDeFranjaAAsignar(horaFranja, sede), ct);
        if (await respuestaAsignar.LeerFalloAsync(ct) is { } falloAsignar)
            return string.Format(Mensajes.RechazoDelDominio, falloAsignar);

        return RespuestaJson.Serializar(new SedeDeFranjaResumen(
            sede is null ? Mensajes.ResultadoSedeRetirada : Mensajes.ResultadoSedeAsignada,
            fichaTurno.Nombre,
            NotacionFranja.Hora(horaFranja),
            sede?.Nombre,
            Mensajes.NotaVisibilidadEventual));
    }
}

/// <summary>
/// Eco compacto de asignar_sede_franja hacia el asistente: el 202 del dominio no trae body, asi
/// que el resultado se compone con lo que se envio a la tool. Sede viaja null al retirar y
/// RespuestaJson la omite del JSON (DefaultIgnoreCondition.WhenWritingNull) -- CA-2 exige que el
/// eco no traiga esa clave.
/// </summary>
public sealed record SedeDeFranjaResumen(string Resultado, string Turno, string Franja, string? Sede, string Nota);
