using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantilla;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

internal sealed record AsignacionDeDia(int Dia, string? TurnoId, FranjaInline? Franja);

internal sealed record MensajesDeCreacion(
    string RechazoDelDominio, string TurnosNoExisten, MensajesDeTurnoEnLinea EnLinea);

internal sealed record PlantillaCreada(
    Guid PlantillaId,
    int DiasAsignados,
    IReadOnlyList<DiaDeCicloRechazado> DiasRechazados,
    IReadOnlyList<TurnoInlineResumen> TurnosEnLinea,
    IReadOnlyDictionary<int, string> TurnoPorDia);

internal sealed record ResultadoCreacion(string? Rechazo, PlantillaCreada? Plantilla);

// Nucleo de creacion compartido por crear_plantilla y crear_plantilla_rotativa (MEF-ADR-0018): resuelve
// turnos por id y franja en linea antes de escribir, hace el POST y un PUT por dia en secuencia (tocan el
// mismo stream); el rechazo de un dia no detiene al resto (CA-ADR-0030). La Jornada la valida cada tool.
internal sealed class CreadorDePlantillaTurnos(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    public async Task<ResultadoCreacion> CrearAsync(
        string nombre, int diasDelCiclo, IReadOnlyList<AsignacionDeDia> asignaciones, Guid? jornadaId,
        MensajesDeCreacion mensajes, CancellationToken ct)
    {
        var turnoIdPorReferencia = new Dictionary<string, string>();
        var nombreDeTurnoPorReferencia = new Dictionary<string, string>();
        var inexistentes = new List<string>();

        foreach (var idTurno in asignaciones.Where(a => a.Franja is null).Select(a => a.TurnoId!).Distinct())
        {
            var resolucion = await resolutorPorId.ResolverAsync(Guid.Parse(idTurno), ct);
            if (resolucion.FalloDeLectura is { } falloLectura)
                return Rechazado(string.Format(mensajes.RechazoDelDominio, falloLectura));
            if (resolucion.Ficha is { } ficha)
            {
                turnoIdPorReferencia[idTurno] = ficha.Id;
                nombreDeTurnoPorReferencia[idTurno] = ficha.Nombre;
            }
            else
                inexistentes.Add(idTurno);
        }

        if (inexistentes.Count > 0)
            return Rechazado(string.Format(mensajes.TurnosNoExisten, string.Join(", ", inexistentes)));

        var preparados = await new PreparadorDeTurnosEnLinea(programacion, resolutor).PrepararAsync(
            [.. asignaciones.Where(a => a.Franja is not null).Select(a => a.Franja!)], mensajes.EnLinea, ct);
        if (preparados.Rechazo is { } rechazoInline)
            return Rechazado(rechazoInline);

        foreach (var (nombreInline, idInline) in preparados.TurnoIdPorNombre)
        {
            turnoIdPorReferencia[nombreInline] = idInline;
            nombreDeTurnoPorReferencia[nombreInline] = nombreInline;
        }

        var plantillaId = Guid.CreateVersion7();
        var respuestaPost = await programacion.CrearPlantillaTurnos(plantillaId, nombre, diasDelCiclo, jornadaId, ct);
        if (await respuestaPost.LeerFalloAsync(ct) is { } falloPost)
            return Rechazado(string.Format(mensajes.RechazoDelDominio, falloPost));

        var diasAsignados = 0;
        var diasRechazados = new List<DiaDeCicloRechazado>();
        var turnoPorDia = new Dictionary<int, string>();

        foreach (var asignacion in asignaciones)
        {
            var referencia = asignacion.Franja?.Nombre ?? asignacion.TurnoId!;
            var nombreTurno = nombreDeTurnoPorReferencia[referencia];
            turnoPorDia[asignacion.Dia] = nombreTurno;

            var respuestaPut = await programacion.AsignarTurnoADiaDePlantillaTurnos(
                plantillaId.ToString(), asignacion.Dia, turnoIdPorReferencia[referencia], ct);

            if (await respuestaPut.LeerFalloAsync(ct) is { } motivo)
                diasRechazados.Add(new DiaDeCicloRechazado(asignacion.Dia, nombreTurno, motivo));
            else
                diasAsignados++;
        }

        return new ResultadoCreacion(
            null,
            new PlantillaCreada(plantillaId, diasAsignados, diasRechazados, preparados.Resumen, turnoPorDia));
    }

    private static ResultadoCreacion Rechazado(string texto) => new(texto, null);
}
