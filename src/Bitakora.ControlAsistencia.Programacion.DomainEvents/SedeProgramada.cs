namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>Sede efectiva donde rige la programacion del turno para el dia solicitado.</summary>
/// <remarks>
/// Snapshot resuelto por el cliente: el servidor NUNCA consulta ni valida contra el maestro de sedes y
/// la verdad queda grabada en el evento. Id es opaco (puede o no ser un guid); Nombre viaja para que el
/// evento quede autocontenido; CentroDeCostos es opcional y opaco. No se llama "Sede": ese nombre queda
/// reservado para el concepto rico del maestro de sedes. Sin Equals custom: todos los campos son string.
/// </remarks>
public record SedeProgramada(string Id, string Nombre, string? CentroDeCostos = null)
{
    /// <summary>
    /// Completa cuando trae ambos datos: el id opaco del cliente y el nombre con que quedara
    /// autocontenida en el evento.
    /// </summary>
    public bool EstaCompleta() =>
        !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Nombre);

    /// <summary>
    /// Copia con el centro de costos canonico: null cuando no hay dato, nunca vacio o en blanco. Se
    /// invoca UNA vez, en el punto de entrada del BC; aguas abajo solo se transporta el valor normalizado.
    /// </summary>
    public SedeProgramada ConCentroDeCostosNormalizado() =>
        string.IsNullOrWhiteSpace(CentroDeCostos) ? this with { CentroDeCostos = null } : this;
}
