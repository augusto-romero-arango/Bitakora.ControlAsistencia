using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.QuitarSubFranja;

// Cliente HTTP puro que envuelve GET programacion/turnos (resolver nombre -> id + ficha vigente
// para el eco) + POST programacion/turnos/{id}:quitar-subfranja (paso 4 MEF-ADR-0043). El
// 400/404/409 del POST se traduce a texto, nunca a excepcion (CA-ADR-0030).
public partial class QuitarSubFranjaTool(ProgramacionApi programacion)
{
    internal const string NombreTool = "quitar_subfranja";

    private readonly ResolutorTurnoPorNombre resolutor = new(programacion);

    [Function("QuitarSubFranja")]
    public async Task<string> Run(
        [McpToolTrigger(
            NombreTool,
            "Quita de una franja ordinaria de un turno el descanso o extra que empieza a la hora "
            + "indicada. Indica el turno por su nombre, la franja por su hora de inicio, el tipo "
            + "(descanso o extra) y la hora de inicio de la sub-franja. Para corregir un horario, "
            + "quita y agrega de nuevo.")]
        [McpMetadata("""{"readOnlyHint": false, "destructiveHint": true}""")]
        ToolInvocationContext context,
        [McpToolProperty(
            "turno", "Nombre exacto del turno del catalogo (miralo con listar_turnos).", isRequired: true)]
        string turno,
        [McpToolProperty(
            "franja", "Hora de inicio de la franja ordinaria que contiene la sub-franja, formato HH:mm.",
            isRequired: true)]
        string franja,
        [McpToolProperty("tipo", "Tipo de sub-franja: 'descanso' o 'extra'.", isRequired: true)]
        string tipo,
        [McpToolProperty("inicio", "Hora de inicio del descanso o extra a quitar, formato HH:mm.", isRequired: true)]
        string inicio,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(turno))
            return string.Format(Mensajes.CampoObligatorio, "turno");
        if (string.IsNullOrWhiteSpace(franja))
            return string.Format(Mensajes.CampoObligatorio, "franja");
        if (string.IsNullOrWhiteSpace(tipo))
            return string.Format(Mensajes.CampoObligatorio, "tipo");
        if (string.IsNullOrWhiteSpace(inicio))
            return string.Format(Mensajes.CampoObligatorio, "inicio");

        var tipoNormalizado = tipo.Trim().ToLowerInvariant();
        if (tipoNormalizado is not ("descanso" or "extra"))
            return string.Format(Mensajes.TipoDesconocido, tipo);

        if (!NotacionFranja.TryParseHora(franja, out var horaFranja))
            return string.Format(Mensajes.HoraInvalida, "franja", franja);
        if (!NotacionFranja.TryParseHora(inicio, out var horaInicio))
            return string.Format(Mensajes.HoraInvalida, "inicio", inicio);

        var resolucion = await resolutor.ResolverAsync(turno, ct);
        if (resolucion.FalloDeLectura is { } falloTurnos)
            return string.Format(Mensajes.RechazoDelDominio, falloTurnos);
        if (resolucion.Ficha is null)
            return string.Format(
                Mensajes.TurnoNoExiste, turno, string.Join(", ", resolucion.NombresDisponibles));
        var fichaTurno = resolucion.Ficha;

        var respuestaQuitar = await programacion.QuitarSubFranja(
            fichaTurno.Id, new SubFranjaAQuitar(horaFranja, tipoNormalizado, horaInicio), ct);
        if (await respuestaQuitar.LeerFalloAsync(ct) is { } falloQuitar)
            return string.Format(Mensajes.RechazoDelDominio, falloQuitar);

        return RespuestaJson.Serializar(new SubFranjaQuitadaResumen(
            Mensajes.ResultadoSubFranjaQuitada,
            fichaTurno.Nombre,
            NotacionFranja.Hora(horaFranja),
            ComponerEco(fichaTurno, horaFranja, tipoNormalizado, horaInicio),
            Mensajes.NotaVisibilidadEventual));
    }

    // El eco usa lo que la ficha vigente mostraba al momento de la llamada (visibilidad eventual):
    // si aun no mostraba la sub-franja, solo tipo + hora -- el POST ya se envio igual y el dominio
    // decide con 409 si en verdad no existia.
    private static string ComponerEco(FichaTurno ficha, TimeOnly horaFranja, string tipo, TimeOnly horaInicio)
    {
        var franjaFicha = ficha.Franjas.FirstOrDefault(f => f.HoraInicio == horaFranja);
        var lista = tipo == "extra" ? franjaFicha?.Extras : franjaFicha?.Descansos;
        var subFranja = lista?.FirstOrDefault(s => s.HoraInicio == horaInicio);

        return subFranja is not null
            ? $"{tipo} {NotacionFranja.Rango(subFranja.HoraInicio, subFranja.HoraFin, subFranja.DiaOffsetInicio, subFranja.DiaOffsetFin)}"
            : $"{tipo} {NotacionFranja.Hora(horaInicio)}";
    }
}

/// <summary>
/// Eco compacto de quitar_subfranja hacia el asistente: la sub-franja quitada se compone con lo
/// que mostraba la ficha vigente al momento de la llamada, en notacion compacta.
/// </summary>
public sealed record SubFranjaQuitadaResumen(
    string Resultado, string Turno, string Franja, string SubFranjaQuitada, string Nota);
